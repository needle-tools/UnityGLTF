using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if HAVE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace UnityGLTF
{
    public static class MaterialBaker
    {
        private static Material _dilateMaterial;

        public class TextureWithTransform
        {
            public Texture2D map = null;
            public Vector2 offset = Vector2.zero;
            public Vector2 scale = Vector2.one;
            
            public bool hasDefaultTransform { get => offset == Vector2.zero && scale == Vector2.one; }

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
                    
                    if (EditorGUI.DropdownButton(new Rect(position.x + EditorGUIUtility.labelWidth, position.y, propWidth, position.height), new GUIContent(widthProp.intValue.ToString()), FocusType.Keyboard))
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
                    if (EditorGUI.DropdownButton(new Rect(position.x + EditorGUIUtility.labelWidth  + propWidth + 2, position.y, propWidth, position.height), new GUIContent(heightProp.intValue.ToString()), FocusType.Keyboard))
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
                    
                    if (GUI.Button(new Rect(position.width - 30 , position.y, 30, position.height), new GUIContent("▢", "Select a squared resolution.")))
                    {
                        var menu = new GenericMenu();
                        foreach (var res in ResolutionOptions)
                        {
                            menu.AddItem(new GUIContent(res + " x "+res), widthProp.intValue == res && heightProp.intValue == res, () =>
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
        
        public enum BakeMode
        {
            TextureSpace,
            UV0,
            UV1,
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

        public static PbrMaps[] Bake(Renderer renderer, BakeSettings settings)
        {
            var pbrMaps = new List<PbrMaps>();
            PbrMaps newPbrMaps = null;
            switch (settings.bakeMode)
            {
                case BakeMode.TextureSpace:
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        newPbrMaps = BakePBRMaterial(renderer, i, settings.resolution.width, settings.resolution.height);
                        if (newPbrMaps != null)
                            pbrMaps.Add(newPbrMaps);
                    }
                    break;
                case BakeMode.UV0:
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        newPbrMaps = BakePBRMaterial(renderer, i, settings.resolution.width, settings.resolution.height, 0);
                        if (newPbrMaps != null)
                            pbrMaps.Add(newPbrMaps);
                    }
                    break;
                case BakeMode.UV1:
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        newPbrMaps = BakePBRMaterial(renderer, i, settings.resolution.width, settings.resolution.height, 1);
                        if (newPbrMaps != null)
                            pbrMaps.Add(newPbrMaps);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return pbrMaps.ToArray();
        }

        private static void DilateMap(Texture source, RenderTexture target, Color backgroundColor)
        {
            if (!_dilateMaterial)
            {
                _dilateMaterial = new Material(Shader.Find("TextureBake/Dilate"));
                if (!_dilateMaterial)
                {
                    Debug.LogError("Failed to create dilate material. Shader not found: TextureBake/Dilate");
                    return;
                }
                
            }
            _dilateMaterial.SetColor("_BackgroundColor", backgroundColor);
            _dilateMaterial.SetTexture("_MainTex", source);
            Graphics.Blit(source, target, _dilateMaterial);
        }
        
        public static PbrMaps BakePBRMaterial(Material material, int width, int height)
        {
            var pbrMaps = new PbrMaps();
            pbrMaps.forMaterial = material;
#if HAVE_URP
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.SpriteMask, width, height, out pbrMaps.mask);
            var mask = pbrMaps.mask?.map;
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.Albedo, width, height, out pbrMaps.albedo, mask);
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.Alpha, width, height, out pbrMaps.alpha, mask);
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.Metallic, width, height, out pbrMaps.metallic, mask);
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.NormalTangentSpace, width, height, out pbrMaps.normal, mask);
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.AmbientOcclusion, width, height, out pbrMaps.occlusion, mask);
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.Emission, width, height, out pbrMaps.emission, mask);
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.Smoothness, width, height, out pbrMaps.smoothness, mask);
            BakeUrpMaterialModeToTexture(material, DebugMaterialMode.Specular, width, height, out pbrMaps.specular, mask);
#endif
            return pbrMaps;
        }

        public static PbrMaps BakePBRMaterial(Renderer renderer, int submesh, int width, int height, int uvChannel = 0)
        {
            var pbrMaps = new PbrMaps();
            var materials = renderer.sharedMaterials;
            pbrMaps.forMaterial = materials[submesh % materials.Length];
            pbrMaps.forMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            
            foreach (var shader in PatchedShaders)
            {
                var pair = shader.Value;
                if (!pair) continue;
                Object.DestroyImmediate(pair);
            }
            PatchedShaders.Clear();
            MeshUVs.Clear();
            
#if HAVE_URP
            pbrMaps.mask = Bake(renderer, submesh, DebugMaterialMode.SpriteMask, width, height, uvChannel);
            var mask = pbrMaps.mask?.map;
            pbrMaps.albedo = Bake(renderer, submesh, DebugMaterialMode.Albedo, width, height, uvChannel, mask);
            pbrMaps.alpha = Bake(renderer, submesh, DebugMaterialMode.Alpha, width, height, uvChannel, mask);
            pbrMaps.metallic = Bake(renderer, submesh, DebugMaterialMode.Metallic, width, height, uvChannel, mask);
            pbrMaps.normal = Bake(renderer, submesh, DebugMaterialMode.NormalTangentSpace, width, height, uvChannel, mask);
            pbrMaps.occlusion = Bake(renderer, submesh, DebugMaterialMode.AmbientOcclusion, width, height, uvChannel, mask);
            pbrMaps.emission = Bake(renderer, submesh, DebugMaterialMode.Emission, width, height, uvChannel, mask);
            pbrMaps.smoothness = Bake(renderer, submesh, DebugMaterialMode.Smoothness, width, height, uvChannel, mask);
            pbrMaps.specular = Bake(renderer, submesh, DebugMaterialMode.Specular, width, height, uvChannel, mask);
#endif
            return pbrMaps;
        }
    
        private static readonly Dictionary<(Shader shader, int uvChannel), Shader> PatchedShaders = new Dictionary<(Shader shader, int uvChannel), Shader>();
        private static readonly Dictionary<(Mesh mesh, int uvChannel), (Vector2 minMaxX, Vector2 minMaxY)> MeshUVs = new Dictionary<(Mesh mesh, int uvChannel), (Vector2 minMaxX, Vector2 minMaxY)>();
        
#if HAVE_URP
        /// <summary>
        /// Bakes a texture from the given renderer's submesh using the specified debug material mode.
        /// Returns null if the baked texture is empty.
        /// </summary>
        public static TextureWithTransform Bake(Renderer renderer, int submesh, DebugMaterialMode mode, int width, int height, int uvChannel, Texture2D mask = null)
        {
            DeactivateGlobalUrpDebugProperties();
            
            // TODO: submeshes
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var trs = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, renderer.transform.lossyScale);
            var materials = renderer.sharedMaterials;
            var sourceMaterial = materials[submesh % materials.Length];

            bool useHdr = mode == DebugMaterialMode.Emission;
            
            var isLinear = IsDebugMaterialModeInLinear(mode);
//            if (mode == DebugMaterialMode.Emission)
//                 isLinear = true;
            
            var rt = RenderTexture.GetTemporary(width, height, 0, useHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            
            var material = Object.Instantiate(sourceMaterial);
            material.hideFlags = HideFlags.DontSave;
            
            // HACK: disable a view-dependant effect on a particular shader
            if (material.HasFloat("_Fresnel_Normal_Overide"))
                material.SetFloat("_Fresnel_Normal_Overide", 0f);
            
            var pair = (material.shader, uvChannel);
            if (!PatchedShaders.TryGetValue(pair, out var patched))
            {
                var patchedShader = ShaderModifier.PatchShaderUVsToClipSpace(material.shader, uvChannel);
                PatchedShaders[pair] = patchedShader;
                material.shader = patchedShader;
            }
            else
            {
                material.shader = patched;
            }
          
            var cmd = new CommandBuffer();
            GL.sRGBWrite = !isLinear;
            
            cmd.SetRenderTarget(rt);

            Color backgroundColor = Color.black;
            switch (mode)
            {
                case DebugMaterialMode.NormalTangentSpace:
                    backgroundColor = new Color(0.5f, 0.5f, 1f, 1f);
                    break;
                case DebugMaterialMode.SpriteMask:
                    backgroundColor = Color.clear;
                    break;
            }
            cmd.ClearRenderTarget(true, true, backgroundColor);
            
            // TODO we probably need to find the UV extents of the source mesh and set the viewport accordingly; otherwise we end up with a wrong space here.
            // We also might need to adjust the texture transform of the material to match the UV extents after baking,
            // so we don't have to modify UV coordinates of the mesh.
            var meshRangePair = (mesh, uvChannel);
            if (!MeshUVs.TryGetValue(meshRangePair, out var minMax))
            {
                Vector2[] meshUVs = null;
                switch (uvChannel)
                {
                    case 0 : 
                        meshUVs = mesh.uv;
                        break;
                    case 1 :
                        meshUVs = mesh.uv2;
                        break;
                    case 2 :
                        meshUVs = mesh.uv3;
                        break;
                    case 3 :
                        meshUVs = mesh.uv4;
                        break;
                    case 4 :
                        meshUVs = mesh.uv5;
                        break;
                }
                var xRange = new Vector2(float.MaxValue, float.MinValue);
                var yRange = new Vector2(float.MaxValue, float.MinValue);
                foreach (var uv in meshUVs)
                {
                    xRange.x = Mathf.Min(xRange.x, uv.x);
                    xRange.y = Mathf.Max(xRange.y, uv.x);
                    yRange.x = Mathf.Min(yRange.x, uv.y);
                    yRange.y = Mathf.Max(yRange.y, uv.y);
                }
                minMax = (xRange, yRange);
                MeshUVs[meshRangePair] = minMax;
            }
            
            var minMaxX = minMax.minMaxX;
            var minMaxY = minMax.minMaxY;
            Vector2 offset = Vector2.zero;
            Vector2 scale = Vector2.one;
            // Regular case – UVs are in 0..1 range. We might not want to introduce texture transforms for this case.
            if (minMaxX.x >= 0 && minMaxX.y <= 1 && minMaxY.x > 0 && minMaxY.y <= 1)
            {
                minMaxX.x = 0;
                minMaxX.y = 1;
                minMaxY.x = 0;
                minMaxY.y = 1;
            }
            else
            {
                float xSize = Mathf.Abs(minMaxX.y - minMaxX.x);
                float ySize = Mathf.Abs(minMaxY.y - minMaxY.x);
            
                float xScale = 1f / xSize;
                float yScale = 1f / ySize;
                offset = new Vector2(-minMaxX.x * xScale, -minMaxY.x * yScale);
                scale = new Vector2(xScale, yScale);
            }
            
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(minMaxX.x, minMaxX.y, minMaxY.x, minMaxY.y, -1, 1));
            cmd.EnableKeyword(new GlobalKeyword(ShaderKeywordStrings.DEBUG_DISPLAY));
            cmd.SetGlobalFloat("_DebugMaterialMode", (int) mode);
            cmd.DrawMesh(mesh, Matrix4x4.identity, material, submesh, 0);
          
          //  cmd.DrawMesh(mesh, trs, material, submesh, 0);
            GL.sRGBWrite = !isLinear;
            Graphics.ExecuteCommandBuffer(cmd);
            //
            // cmd.Clear();
            // cmd.DisableKeyword(new GlobalKeyword(ShaderKeywordStrings.DEBUG_DISPLAY));
            // Graphics.ExecuteCommandBuffer(cmd);
            //
            
            var dilateRt = RenderTexture.GetTemporary(width, height, 0, useHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);

            DilateMap(rt, dilateRt, backgroundColor);
            
            RenderTexture.active = dilateRt;
            // if (mode == DebugMaterialMode.Emission)
            //     isLinear = false;
            
            var bakedTexture = new Texture2D(width, height, useHdr ? TextureFormat.RGBAFloat : TextureFormat.RGB24, false, isLinear);
            bakedTexture.wrapMode = TextureWrapMode.Repeat;
            bakedTexture.filterMode = FilterMode.Bilinear;
            bakedTexture.anisoLevel = 1;
            
            bakedTexture.Apply();
            // Read pixels from renderTexture and apply to bakedTexture
            GL.sRGBWrite = !isLinear;
            bakedTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
      
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(dilateRt);
            RenderTexture.ReleaseTemporary(rt);
            
            Shader.DisableKeyword(ShaderKeywordStrings.DEBUG_DISPLAY);

            if (IsTextureEmpty(bakedTexture, mask))
            {
                Object.DestroyImmediate(bakedTexture);
                return null;
            }
            
            return new TextureWithTransform(bakedTexture, offset, scale);
        }
        
        public static bool TextureHasSingleValue(Texture2D texture, out Color singleValue, Texture2D mask = null)
        {
            singleValue = Color.clear;
            if (!texture)
                return false;
            
            var pixelData = texture.GetPixels();
            
            var maskData = mask?.GetPixels();

            Color? lastColor = null;
            
            for (int i = 0; i < pixelData.Length; i++)
            {
                if (maskData != null && maskData[i] == Color.black)
                    continue; // skip masked pixels
                
                if (lastColor.HasValue && pixelData[i] != lastColor.Value)
                {
                    singleValue = Color.clear;
                    return false; // found different color
                }
                else
                {
                    lastColor = pixelData[i];
                }
                
            }
            if (lastColor == null)
            {
                singleValue = Color.clear;
                return false; // no pixels found
            }
            
            singleValue = lastColor.Value;

            return true;
        }

        private static bool IsTextureEmpty(Texture2D texture, bool ignoreAlpha = true, Texture2D mask = null)
        {
            var pixelData = texture.GetPixelData<Color32>(0);
            var maskData = mask?.GetPixels();

            bool hasData = false;
            for (int i = 0; i < pixelData.Length; i++)
            {
                if (maskData != null && maskData[i] == Color.black)
                    continue; // skip masked pixels

                hasData |= (!ignoreAlpha && pixelData[i].a != 0) || pixelData[i].r != 0 || pixelData[i].g != 0 || pixelData[i].b != 0;
                if (hasData)
                    break;
            }

            return !hasData;
        }
        
        private static void DeactivateGlobalUrpDebugProperties()
        {
            // See DebugHandler.cs in URP package
            
            Shader.SetGlobalFloat("_DebugVertexAttributeMode", 0);

            Shader.SetGlobalInteger("_DebugMaterialValidationMode", 0);

            // Rendering settings...
            Shader.SetGlobalInteger("k_DebugMipInfoModeId", 0);
            Shader.SetGlobalInteger("_DebugSceneOverrideMode", 0);
            Shader.SetGlobalInteger("_DebugFullScreenMode", 0);
            Shader.SetGlobalInteger("_DebugValidationMode", 0);

            // Lighting settings...
            Shader.SetGlobalFloat("_DebugLightingMode", 0);
            Shader.SetGlobalInteger("_DebugLightingFeatureFlags", 0);
        }

        private static bool IsDebugMaterialModeInLinear(DebugMaterialMode mode)
        {
            bool isLinear = false;
            switch (mode)
            {
                case DebugMaterialMode.Alpha:
                case DebugMaterialMode.Smoothness:
                case DebugMaterialMode.AmbientOcclusion:
                case DebugMaterialMode.NormalWorldSpace:
                case DebugMaterialMode.NormalTangentSpace:
                case DebugMaterialMode.LightingComplexity:
                case DebugMaterialMode.Metallic:
                case DebugMaterialMode.SpriteMask:
                    isLinear = true;
                    break;
            }
            return isLinear;
        }

        private static bool HasAlpha(DebugMaterialMode mode)
        {
            switch (mode)
            {
                case DebugMaterialMode.Albedo:
                case DebugMaterialMode.Alpha:
                case DebugMaterialMode.SpriteMask:
                    return true;
                case DebugMaterialMode.Specular:
                case DebugMaterialMode.Smoothness:
                case DebugMaterialMode.AmbientOcclusion:
                case DebugMaterialMode.Emission:
                case DebugMaterialMode.NormalWorldSpace:
                case DebugMaterialMode.NormalTangentSpace:
                case DebugMaterialMode.LightingComplexity:
                case DebugMaterialMode.Metallic:
                    return false;
                default:
                    return true;
            }
        }
        
        private static void BakeUrpMaterialModeToTexture(Material mat, DebugMaterialMode mode, int textureWidth, int textureHeight, out TextureWithTransform baked, Texture2D mask = null)
        {
            bool isLinear = IsDebugMaterialModeInLinear(mode);
            var material = new Material(mat);
            // HACK: disable a view-dependant effect on a particular shader
            if (material.HasFloat("_Fresnel_Normal_Overide"))
                material.SetFloat("_Fresnel_Normal_Overide", 0f);
            
            var resetTextureTransforms = false;
            if (resetTextureTransforms)
            {
                // reset texture transform properties
                var props = new string[] {
                    "Base_Tiling_Offset",
                    "Global_Tiling_Offset",
                    "_Detail_Tiling_Offset",
                    "_Normal_Detail_Tiling_Offset",
                    "_Normal_Tiling_Offset",
                    "_Smoothness_Detail_Tiling_Offset",
                    "_Smoothness_Tiling_Offset",
                    "_Metallic_Tiling_Offset",
                };
                foreach (var prop in props)
                {
                    if (mat.HasProperty(prop))
                    {
                        material.SetColor(prop, new Color(1, 1, 0, 0));
                    }
                }
            }
            
            Shader.EnableKeyword(ShaderKeywordStrings.DEBUG_DISPLAY);
            Shader.SetGlobalFloat("_DebugMaterialMode", (int)mode);
      
            DeactivateGlobalUrpDebugProperties();
            
            var bakedTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, isLinear);
            bakedTexture.wrapMode = TextureWrapMode.Repeat;
            bakedTexture.filterMode = FilterMode.Bilinear;
            bakedTexture.anisoLevel = 1;
            bakedTexture.Apply();
            GL.sRGBWrite = !isLinear;
            
            // Render mesh with bakeMat to bakedTexture
            RenderTexture renderTexture = RenderTexture.GetTemporary(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            Graphics.Blit(bakedTexture, renderTexture, material, 0);
           
            RenderTexture.active = renderTexture;

            // Read pixels from renderTexture and apply to bakedTexture
            bakedTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
      
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);
            Object.DestroyImmediate(material);
            
            Shader.DisableKeyword(ShaderKeywordStrings.DEBUG_DISPLAY);

            if (IsTextureEmpty(bakedTexture, mask))
            {
                Object.DestroyImmediate(bakedTexture);
                baked = null;
            }
            else
                baked = new TextureWithTransform(bakedTexture);
        }
#endif
    }
}