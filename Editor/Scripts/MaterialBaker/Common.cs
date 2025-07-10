using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityGLTF;
using Object = UnityEngine.Object;
#if HAVE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace UnityGLTF
{
    public enum BakeMode
    {
        TextureSpace,
        UV0,
        UV1,
    }

    public enum MaterialMode
    {
        Albedo,
        Specular,
        Alpha,
        Smoothness,
        AmbientOcclusion,
        Emission,
        NormalWorldSpace,
        NormalTangentSpace,
        Metallic,
        SpriteMask,
    }

    public class TextureContentInfo
    {
        public bool isSingleColor = false;
        public Color singleColor;
        public bool isEmpty = false;
    }
    
    public class TextureWithTransform
    {
        public Texture2D map = null;
        public Vector2 offset = Vector2.zero;
        public Vector2 scale = Vector2.one;

        public TextureContentInfo contentInfo = null;
        public MaterialMode materialMode = MaterialMode.Albedo;
        
        public bool hasDefaultTransform
        {
            get => offset == Vector2.zero && scale == Vector2.one;
        }

        public TextureWithTransform(MaterialMode mode)
        {
            this.materialMode = mode;
        }

        public TextureWithTransform(MaterialMode mode, Texture2D map)
        {
            this.map = map;
            this.materialMode = mode;
        }

        public TextureWithTransform(MaterialMode mode, Texture2D map, Vector2 offset, Vector2 scale)
        {
            this.materialMode = mode;
            this.map = map;
            this.offset = offset;
            this.scale = scale;
        }
    }

    public class PbrMaps : IEnumerable<TextureWithTransform>
    {
        public bool ignore = false;
        public TextureWithTransform mask = new TextureWithTransform(MaterialMode.SpriteMask);
        public TextureWithTransform albedo = new TextureWithTransform(MaterialMode.Albedo);
        public TextureWithTransform alpha = new TextureWithTransform(MaterialMode.Alpha);
        public TextureWithTransform metallic = new TextureWithTransform(MaterialMode.Metallic);
        public TextureWithTransform normal = new TextureWithTransform(MaterialMode.NormalTangentSpace);
        public TextureWithTransform occlusion = new TextureWithTransform(MaterialMode.AmbientOcclusion);
        public TextureWithTransform emission = new TextureWithTransform(MaterialMode.Emission);
        public TextureWithTransform smoothness = new TextureWithTransform(MaterialMode.Smoothness);
        public TextureWithTransform specular = new TextureWithTransform(MaterialMode.Specular);

        public Material forMaterial;
        public Mesh forMesh;

        public bool HasMapSingleColor(MaterialMode mode, out Color color)
        {
            color = Color.clear;
            foreach (var t in this)
            {
                if (t == null || t.map == null)
                    continue;

                if (t.materialMode == mode && t.map != null)
                {
                    if (t.contentInfo != null)
                    {
                        color = t.contentInfo.singleColor;
                        return t.contentInfo.isSingleColor;
                    }

                    return false;
                }
            }

            return false;
        }
        
        public bool HasMap(MaterialMode mode)
        {
            foreach (var t in this)
            {
                if (t == null || t.map == null)
                    continue;
                
                if ( t.materialMode == mode && t.map != null)
                {
                    if (t.contentInfo != null)
                        return !t.contentInfo.isEmpty;
                    
                    return true;
                }
            }

            return false;
        }

        public TextureResolution GetTextureSize()
        {
            foreach (var t in this)
            {
                if (t.map == null)
                    continue;

                if (t.map.width <= 0 || t.map.height <= 0)
                    continue;

                return new TextureResolution(t.map.width, t.map.height);
            }
  
            return new TextureResolution(0, 0);
        }

        public void CleanAllEmptyMaps()
        {
            foreach (var t in this)
            {
                if (t.contentInfo != null && t.map != null)
                {
                    if (t.contentInfo.isEmpty)
                    {
                        Object.DestroyImmediate(t.map);
                        t.map = null;
                    }
                }
            }
            
        }

        public void CollectTextureContentInfo()
        {
            var handles = new List<JobHandle>();
            var results = new Dictionary<TextureWithTransform, ICollectTextureContentInfoResult>();

            foreach (var t in this)
            {
                if (t == mask) continue;
                if (t.map == null) continue;

                if (t.contentInfo == null)
                {
                    handles.Add(CreateJob(t, mask, out var jobResult));
                    results[t] = jobResult;
                }
            }
            
            if (handles.Count == 0)
                return;
            
            while (true)
            {
                if (handles.TrueForAll(h => h.IsCompleted))
                    break;
            }

            foreach (var handle in handles)
                handle.Complete();
           
            foreach (var r in results)
            {
                r.Key.contentInfo = new TextureContentInfo
                {
                    isSingleColor = r.Value.HasSingleColor,
                    singleColor = r.Value.SingleColor,
                    isEmpty = r.Key.materialMode == MaterialMode.NormalTangentSpace ?
                        BakeHelpers.ColorProximity(r.Value.SingleColor, new Color(0.5f, 0.5f, 1f, 1f))
                        : r.Value.IsEmpty

                };
                if (r.Value is IDisposable disposable)
                    disposable.Dispose();
            }
            
        }

        private static unsafe JobHandle CreateJob(TextureWithTransform texture, TextureWithTransform maskTexture, out ICollectTextureContentInfoResult result)
        {
            NativeArray<Color24RGB> mask = default;
            if (maskTexture != null && maskTexture.map != null)
            {
                mask = maskTexture.map.GetPixelData<Color24RGB>(0);
            }
            
            if (texture.map.format == TextureFormat.RGB24)
            {
                var job = new CollectTextureContentInfoJobRGB24
                {
                    singleColorArray = new NativeArray<Color24RGB>(1, Allocator.TempJob),
                    hasSingleColor = new NativeArray<bool>(1, Allocator.TempJob),
                    mask =  mask,
                    textureData = texture.map.GetPixelData<Color24RGB>(0),
                    length = texture.map.width * texture.map.height,
                    isEmpty = new NativeArray<bool>(1, Allocator.TempJob),
                };
                result = job;
                return job.Schedule();
            }
            else if (texture.map.format == TextureFormat.RGBA32)
            {
                var job = new CollectTextureContentInfoJobRGBA32
                {
                    singleColorArray = new NativeArray<Color32RGBA>(1, Allocator.TempJob),
                    hasSingleColor = new NativeArray<bool>(1, Allocator.TempJob),
                    mask =  mask,
                    textureData = texture.map.GetPixelData<Color32RGBA>(0),
                    length = texture.map.width * texture.map.height,
                    isEmpty = new NativeArray<bool>(1, Allocator.TempJob),
                    ignoreAlpha = BakeHelpers.HasAlpha(texture.materialMode) 
                };
                result = job;
                return job.Schedule();
            }
            else if (texture.map.format == TextureFormat.RGBAFloat)
            {
                var job = new CollectTextureContentInfoJobRGBAFloat
                {
                    singleColorArray = new NativeArray<float4>(1, Allocator.TempJob),
                    hasSingleColor = new NativeArray<bool>(1, Allocator.TempJob),
                    mask =  mask,
                    textureData = texture.map.GetPixelData<float4>(0),
                    length = texture.map.width * texture.map.height,
                    isEmpty = new NativeArray<bool>(1, Allocator.TempJob),
                    ignoreAlpha = BakeHelpers.HasAlpha(texture.materialMode) 
                };
                result = job;
                return job.Schedule();
            }
            else
            {
                Debug.LogError("Unsupported texture format: " + texture.map.format);
                result = null;
                return default;
            }

        }

        public IEnumerator<TextureWithTransform> GetEnumerator()
        {
            if (albedo != null)
                yield return albedo;
            if (alpha != null)
                yield return alpha;
            if (metallic != null)
                yield return metallic;
            if (normal != null)
                yield return normal;
            if (occlusion != null)
                yield return occlusion;
            if (emission != null)
                yield return emission;
            if (smoothness != null)
                yield return smoothness;
            if (specular != null)
                yield return specular;
            if (mask != null)
                yield return mask;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    interface ICollectTextureContentInfoResult
    {
        bool HasSingleColor { get; }
        Color SingleColor { get; }
        bool IsEmpty { get; }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct CollectTextureContentInfoJobRGB24 : IJob, ICollectTextureContentInfoResult, IDisposable
    {
        public NativeArray<Color24RGB> singleColorArray;
        public NativeArray<bool> hasSingleColor;
        public NativeArray<bool> isEmpty;
        [ReadOnly]
        public NativeArray<Color24RGB> mask;
        [ReadOnly]
        public NativeArray<Color24RGB> textureData;
        public int length;
        
        public void Execute()
        {
            var maskPtr = mask.IsCreated ? (Color24RGB*)mask.GetUnsafeReadOnlyPtr() : null;
            
            BurstMethods.HasSingleValueRGB((Color24RGB*)textureData.GetUnsafeReadOnlyPtr(), maskPtr, length, out bool hasSingleValue,
                out Color24RGB singleColor);

            BurstMethods.IsTextureEmptyRGB((Color24RGB*)textureData.GetUnsafeReadOnlyPtr(), maskPtr, length, out bool isEmpty);
            this.isEmpty[0] = isEmpty;
            hasSingleColor[0] = hasSingleValue;
            singleColorArray[0] = singleColor;
        }

        public bool HasSingleColor { get => hasSingleColor[0]; }
        public Color SingleColor { get => singleColorArray[0].ToColor(); }

        public bool IsEmpty { get => isEmpty[0]; }
        
        public void Dispose()
        {
            singleColorArray.Dispose();
            hasSingleColor.Dispose();
            isEmpty.Dispose();
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct CollectTextureContentInfoJobRGBA32 : IJob, ICollectTextureContentInfoResult, IDisposable
    {
        public NativeArray<Color32RGBA> singleColorArray;
        public NativeArray<bool> hasSingleColor;
        public NativeArray<bool> isEmpty;
        [ReadOnly]
        public NativeArray<Color24RGB> mask;
        [ReadOnly]
        public NativeArray<Color32RGBA> textureData;
        public int length;
        public bool ignoreAlpha;
        
        public void Execute()
        {
            var maskPtr = mask.IsCreated ? (Color24RGB*)mask.GetUnsafeReadOnlyPtr() : null;

            BurstMethods.HasSingleValueRGBA((Color32RGBA*)textureData.GetUnsafeReadOnlyPtr(), maskPtr, length, out bool hasSingleValue,
                out Color32RGBA singleColor);
            BurstMethods.IsTextureEmptyRGBA((Color32RGBA*)textureData.GetUnsafeReadOnlyPtr(), maskPtr, length, ignoreAlpha, out bool isEmpty);
            this.isEmpty[0] = isEmpty;

            hasSingleColor[0] = hasSingleValue;
            singleColorArray[0] = singleColor;
        }
        
        public bool HasSingleColor { get => hasSingleColor[0]; }
        public Color SingleColor { get => singleColorArray[0].ToColor(); }
        
        public bool IsEmpty { get => isEmpty[0]; }

        public void Dispose()
        {
            singleColorArray.Dispose();
            hasSingleColor.Dispose();
            isEmpty.Dispose();
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct CollectTextureContentInfoJobRGBAFloat : IJob, ICollectTextureContentInfoResult, IDisposable
    {
        public NativeArray<float4> singleColorArray;
        public NativeArray<bool> hasSingleColor;
        public NativeArray<bool> isEmpty;
        [ReadOnly]
        public NativeArray<Color24RGB> mask;
        [ReadOnly]
        public NativeArray<float4> textureData;
        public int length;
        public bool ignoreAlpha;

        public void Execute()
        {
            var maskPtr = mask.IsCreated ? (Color24RGB*)mask.GetUnsafeReadOnlyPtr() : null;

            BurstMethods.HasSingleValueRGBAFloat((float4*)textureData.GetUnsafeReadOnlyPtr(), maskPtr, length, out bool hasSingleValue,
                out float4 singleColor);
            
            BurstMethods.IsTextureEmptyRGBAFloat((float4*)textureData.GetUnsafeReadOnlyPtr(), maskPtr, length, ignoreAlpha, out bool isEmpty);
            this.isEmpty[0] = isEmpty;

            hasSingleColor[0] = hasSingleValue;
            singleColorArray[0] = singleColor;
        }
        
        public bool HasSingleColor { get => hasSingleColor[0]; }
        public Color SingleColor { get => new Color(singleColorArray[0].x, singleColorArray[0].y, singleColorArray[0].z, singleColorArray[0].w); }
        
        public bool IsEmpty { get => isEmpty[0]; }

        public void Dispose()
        {
            singleColorArray.Dispose();
            hasSingleColor.Dispose();
            isEmpty.Dispose();
        }
    }

    
    [Serializable]
    public struct TextureResolution : IEquatable<TextureResolution>
    {
        public int width;
        public int height;

        public TextureResolution(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

#if UNITY_EDITOR

        [CustomPropertyDrawer(typeof(TextureResolution))]
        public class TextureResolutionDrawer : PropertyDrawer
        {
            private static int[] ResolutionOptions = new int[]
            {
                16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192
            };

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var widthProp = property.FindPropertyRelative("width");
                var heightProp = property.FindPropertyRelative("height");

                // Draw Label
                EditorGUI.PrefixLabel(position, label);

                EditorGUI.BeginProperty(position, label, property);

                var propWidth = (position.width - EditorGUIUtility.labelWidth) / 2 - 40;

                if (EditorGUI.DropdownButton(
                        new Rect(position.x + EditorGUIUtility.labelWidth, position.y, propWidth, position.height),
                        new GUIContent(widthProp.intValue.ToString()), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    foreach (var res in ResolutionOptions)
                    {
                        menu.AddItem(new GUIContent(res.ToString()), widthProp.intValue == res, () =>
                        {
                            widthProp.intValue = res;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.ShowAsContext();
                }

                if (EditorGUI.DropdownButton(
                        new Rect(position.x + EditorGUIUtility.labelWidth + propWidth + 2, position.y, propWidth,
                            position.height), new GUIContent(heightProp.intValue.ToString()), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    foreach (var res in ResolutionOptions)
                    {
                        menu.AddItem(new GUIContent(res.ToString()), heightProp.intValue == res, () =>
                        {
                            heightProp.intValue = res;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.ShowAsContext();
                }

                if (GUI.Button(new Rect(position.width - 30, position.y, 30, position.height),
                        new GUIContent("▢", "Select a squared resolution.")))
                {
                    var menu = new GenericMenu();
                    foreach (var res in ResolutionOptions)
                    {
                        menu.AddItem(new GUIContent(res + " x " + res),
                            widthProp.intValue == res && heightProp.intValue == res, () =>
                            {
                                heightProp.intValue = res;
                                widthProp.intValue = res;
                                property.serializedObject.ApplyModifiedProperties();
                            });
                    }

                    menu.ShowAsContext();
                }

                EditorGUI.EndProperty();
            }
        }
#endif
        public bool Equals(TextureResolution other)
        {
            return width == other.width && height == other.height;
        }

        public override bool Equals(object obj)
        {
            return obj is TextureResolution other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(width, height);
        }
    }

    [Serializable]
    public class BakeSettings : IEquatable<BakeSettings>
    {
        public BakeMode bakeMode = BakeMode.TextureSpace;
        public TextureResolution resolution = new TextureResolution(1024, 1024);
        public Vector2 textureTiling = Vector2.one;

        public bool Equals(BakeSettings other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return bakeMode == other.bakeMode && resolution.Equals(other.resolution);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((BakeSettings)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)bakeMode, resolution);
        }
    }

    public static class MaterialModeExtensions
    {
        public static int ToRPSpecific(this MaterialMode mode)
        {
#if HAVE_URP   // URP
            switch (mode)
            {
                case MaterialMode.Albedo:
                    return (int)DebugMaterialMode.Albedo;
                case MaterialMode.Specular:
                    return (int)DebugMaterialMode.Specular;
                case MaterialMode.Alpha:
                    return (int)DebugMaterialMode.Alpha;
                case MaterialMode.Smoothness:
                    return (int)DebugMaterialMode.Smoothness;
                case MaterialMode.AmbientOcclusion:
                    return (int)DebugMaterialMode.AmbientOcclusion;
                case MaterialMode.Emission:
                    return (int)DebugMaterialMode.Emission;
                case MaterialMode.NormalWorldSpace:
                    return (int)DebugMaterialMode.NormalWorldSpace;
                case MaterialMode.NormalTangentSpace:
                    return (int)DebugMaterialMode.NormalTangentSpace;
                case MaterialMode.Metallic:
                    return (int)DebugMaterialMode.Metallic;
                case MaterialMode.SpriteMask:
                    return (int)DebugMaterialMode.SpriteMask;
                default:
                    Debug.LogError("Unknown MaterialMode: " + mode);
                    return -1; // Invalid mode
            }
#endif
#if HAVE_HDRP
            switch (mode)
            {
                default:
                    Debug.LogError("Unknown MaterialMode: " + mode);
                    return -1; // Invalid mode
            }
#endif
#pragma  warning disable CS0162 
            Debug.LogError("Unsupported RenderPipeline");
            return -1;
#pragma  warning restore CS0162 
        }
    }
}