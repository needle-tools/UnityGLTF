using System;
using UnityEditor;
using UnityEngine;
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

    public class TextureWithTransform
    {
        public Texture2D map = null;
        public Vector2 offset = Vector2.zero;
        public Vector2 scale = Vector2.one;

        public bool hasDefaultTransform
        {
            get => offset == Vector2.zero && scale == Vector2.one;
        }

        public TextureWithTransform()
        {
        }

        public TextureWithTransform(Texture2D map)
        {
            this.map = map;
        }

        public TextureWithTransform(Texture2D map, Vector2 offset, Vector2 scale)
        {
            this.map = map;
            this.offset = offset;
            this.scale = scale;
        }
    }

    public class PbrMaps
    {
        public bool ignore = false;
        public TextureWithTransform albedo;
        public TextureWithTransform alpha;
        public TextureWithTransform metallic;
        public TextureWithTransform normal;
        public TextureWithTransform occlusion;
        public TextureWithTransform emission;
        public TextureWithTransform smoothness;
        public TextureWithTransform specular;
        public TextureWithTransform mask;

        public Material forMaterial;
        public Mesh forMesh;

        public TextureResolution GetTextureSize()
        {
            if (albedo != null)
                return new TextureResolution(albedo.map.width, albedo.map.height);
            if (alpha != null)
                return new TextureResolution(alpha.map.width, alpha.map.height);
            if (metallic != null)
                return new TextureResolution(metallic.map.width, metallic.map.height);
            if (normal != null)
                return new TextureResolution(normal.map.width, normal.map.height);
            if (occlusion != null)
                return new TextureResolution(occlusion.map.width, occlusion.map.height);
            if (emission != null)
                return new TextureResolution(emission.map.width, emission.map.height);
            if (smoothness != null)
                return new TextureResolution(smoothness.map.width, smoothness.map.height);
            if (specular != null)
                return new TextureResolution(specular.map.width, specular.map.height);
            if (mask != null)
                return new TextureResolution(mask.map.width, mask.map.height);

            return new TextureResolution(0, 0);
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