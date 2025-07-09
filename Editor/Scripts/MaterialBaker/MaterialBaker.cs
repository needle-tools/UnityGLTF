using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string DEBUG_DISPLAY = "DEBUG_DISPLAY";

        private static string[] IgnorableShaders = new string[]
        {
            "UnityGLTF/PBRGraph",
            "UnityGLTF/UnlitGraph",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Simple Lit",
        };
        
        private static bool ShouldMaterialBeIgnored(Material material, BakeMode mode)
        {
            if (mode != BakeMode.TextureSpace 
                && (!ShaderModifier.IsShaderGraph(material.shader) && !ShaderModifier.IsAmplifyShader(material.shader, out _)))
                return true;

            if (mode == BakeMode.TextureSpace)
            {
                if (ShaderModifier.IsAmplifyShader(material.shader, out bool hasDebugMode))
                {
                    if (!hasDebugMode)
                        return true;
                }
                
            }

            if (IgnorableShaders.Contains(material.shader.name))
                return true;
            
            return false;
        }
        
        

        public static PbrMaps[] Bake(Renderer renderer, BakeSettings settings)
        {
            var pbrMaps = new List<PbrMaps>();
            var sharedMaterials = renderer.sharedMaterials;
            PbrMaps newPbrMaps = null;
            switch (settings.bakeMode)
            {
                case BakeMode.TextureSpace:
                    for (var i = 0; i < sharedMaterials.Length; i++)
                    {
                        if (ShouldMaterialBeIgnored(sharedMaterials[i], settings.bakeMode))
                        {
                            pbrMaps.Add(new PbrMaps { forMaterial = sharedMaterials[i], ignore = true });
                            continue;
                        }
                        newPbrMaps = BakePBRMaterial(sharedMaterials[i], settings.resolution);
                        if (newPbrMaps != null)
                            pbrMaps.Add(newPbrMaps);
                        else
                            pbrMaps.Add(new PbrMaps { forMaterial = sharedMaterials[i], ignore = true});
                    }
                    break;
                case BakeMode.UV0:
                    for (var i = 0; i < sharedMaterials.Length; i++)
                    {
                        if (ShouldMaterialBeIgnored(sharedMaterials[i], settings.bakeMode))
                        {
                            pbrMaps.Add(new PbrMaps { forMaterial = sharedMaterials[i], ignore = true });
                            continue;
                        }
                        newPbrMaps = BakePBRMaterial(renderer, i, settings.resolution, 0);
                        if (newPbrMaps != null)
                            pbrMaps.Add(newPbrMaps);
                        else
                            pbrMaps.Add(new PbrMaps { forMaterial = sharedMaterials[i], ignore = true });
                    }
                    break;
                case BakeMode.UV1:
                    for (var i = 0; i < sharedMaterials.Length; i++)
                    {
                        if (ShouldMaterialBeIgnored(sharedMaterials[i], settings.bakeMode))
                        {
                            pbrMaps.Add(new PbrMaps { forMaterial = sharedMaterials[i], ignore = true });
                            continue;
                        }
                        newPbrMaps = BakePBRMaterial(renderer, i, settings.resolution, 1);
                        if (newPbrMaps != null)
                            pbrMaps.Add(newPbrMaps);
                        else
                            pbrMaps.Add(new PbrMaps { forMaterial = sharedMaterials[i], ignore = true });
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return pbrMaps.ToArray();
        }
        
        public static PbrMaps BakePBRMaterial(Material material, TextureResolution resolution)
        {
            var pbrMaps = new PbrMaps();
            pbrMaps.forMaterial = material;
            BakeTextureSpace(material, MaterialMode.SpriteMask, resolution, out pbrMaps.mask);
            var mask = pbrMaps.mask?.map;
            BakeTextureSpace(material, MaterialMode.Albedo, resolution, out pbrMaps.albedo, mask);
            BakeTextureSpace(material, MaterialMode.Alpha, resolution, out pbrMaps.alpha, mask);
            BakeTextureSpace(material, MaterialMode.Metallic, resolution, out pbrMaps.metallic, mask);
            BakeTextureSpace(material, MaterialMode.NormalTangentSpace, resolution, out pbrMaps.normal, mask);
            BakeTextureSpace(material, MaterialMode.AmbientOcclusion, resolution, out pbrMaps.occlusion, mask);
            BakeTextureSpace(material, MaterialMode.Emission, resolution, out pbrMaps.emission, mask);
            BakeTextureSpace(material, MaterialMode.Smoothness, resolution, out pbrMaps.smoothness, mask);
            BakeTextureSpace(material, MaterialMode.Specular, resolution, out pbrMaps.specular, mask);
            return pbrMaps;
        }

        public static PbrMaps BakePBRMaterial(Renderer renderer, int submesh, TextureResolution resolution, int uvChannel = 0)
        {
            var pbrMaps = new PbrMaps();
            var materials = renderer.sharedMaterials;
            pbrMaps.forMaterial = materials[submesh % materials.Length];
            pbrMaps.forMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            
            MeshUVs.Clear();
            
            pbrMaps.mask = BakeUVSpace(renderer, submesh, MaterialMode.SpriteMask, resolution, uvChannel);
            var mask = pbrMaps.mask?.map;
            pbrMaps.albedo = BakeUVSpace(renderer, submesh, MaterialMode.Albedo, resolution, uvChannel, mask);
            pbrMaps.alpha = BakeUVSpace(renderer, submesh, MaterialMode.Alpha, resolution, uvChannel, mask);
            pbrMaps.metallic = BakeUVSpace(renderer, submesh, MaterialMode.Metallic, resolution, uvChannel, mask);
            pbrMaps.normal = BakeUVSpace(renderer, submesh, MaterialMode.NormalTangentSpace, resolution, uvChannel, mask);
            pbrMaps.occlusion = BakeUVSpace(renderer, submesh, MaterialMode.AmbientOcclusion, resolution, uvChannel, mask);
            pbrMaps.emission = BakeUVSpace(renderer, submesh, MaterialMode.Emission, resolution, uvChannel, mask);
            pbrMaps.smoothness = BakeUVSpace(renderer, submesh, MaterialMode.Smoothness, resolution, uvChannel, mask);
            pbrMaps.specular = BakeUVSpace(renderer, submesh, MaterialMode.Specular, resolution, uvChannel, mask);
            return pbrMaps;
        }
    
        private static readonly Dictionary<(Shader shader, int uvChannel), (Shader shader, DateTime lastChange)> PatchedShaders = new Dictionary<(Shader shader, int uvChannel), (Shader, DateTime)>();
        private static readonly Dictionary<(Mesh mesh, int uvChannel), (Vector2 minMaxX, Vector2 minMaxY)> MeshUVs = new Dictionary<(Mesh mesh, int uvChannel), (Vector2 minMaxX, Vector2 minMaxY)>();
        
        private static void PatchAndReplaceShader(Material material, int uvChannel)
        {
            var pair = (material.shader, uvChannel);

            var path = AssetDatabase.GetAssetPath(material.shader);
            var lastWriteTime = System.IO.File.GetLastWriteTime(path);

            Shader cachedShader = null;
            
            if (PatchedShaders.TryGetValue(pair, out var cache))
            {
                if (cache.lastChange == lastWriteTime)
                    cachedShader = cache.shader;
            }
            
            if (cachedShader == null)
            {
                var patchedShader = ShaderModifier.PatchShaderUVsToClipSpace(material.shader, uvChannel);
                if (PatchedShaders.ContainsKey(pair))
                    PatchedShaders.Remove(pair);
                PatchedShaders[pair] = (patchedShader, lastWriteTime);
                material.shader = patchedShader;
            }
            else
            {
                material.shader = cachedShader;
            } 
        }
        
        /// <summary>
        /// Bakes a texture from the given renderer's submesh using the specified debug material mode.
        /// Returns null if the baked texture is empty.
        /// </summary>
        public static TextureWithTransform BakeUVSpace(Renderer renderer, int submesh, MaterialMode mode, TextureResolution resolution, int uvChannel, Texture2D mask = null)
        {
            DeactivateGlobalDebugProperties();
            
            // TODO: submeshes
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var materials = renderer.sharedMaterials;
            var sourceMaterial = materials[submesh % materials.Length];
            
            var isLinear = BakeHelpers.IsDebugMaterialModeInLinear(mode);
            var rt = BakeHelpers.CreateRenderTextureForMode(mode, resolution);
            
            var material = Object.Instantiate(sourceMaterial);
            material.hideFlags = HideFlags.DontSave;
            
            // HACK: disable a view-dependant effect on a particular shader
            if (material.HasFloat("_Fresnel_Normal_Overide"))
                material.SetFloat("_Fresnel_Normal_Overide", 0f);
            
            PatchAndReplaceShader(material, uvChannel);
          
            var cmd = new CommandBuffer();
            GL.sRGBWrite = !isLinear;
            
            cmd.SetRenderTarget(rt);

            Color backgroundColor = Color.black;
            bool doDilate = true;
            switch (mode)
            {
                case MaterialMode.NormalTangentSpace:
                    backgroundColor = new Color(0.5f, 0.5f, 1f, 1f);
                    break;
                case MaterialMode.SpriteMask:
                    backgroundColor = Color.clear;
                    doDilate = false;
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
            cmd.EnableShaderKeyword(DEBUG_DISPLAY);
            cmd.SetGlobalFloat("_DebugMaterialMode", (int) mode.ToRPSpecific());
            var forwardPassIndex = FindForwardPassIndex(material);
            cmd.DrawMesh(mesh, Matrix4x4.identity, material, submesh, forwardPassIndex);
            GL.sRGBWrite = !isLinear;
            Graphics.ExecuteCommandBuffer(cmd);
            
            RenderTexture dilateRt = null;
            if (doDilate)
            {
                dilateRt = BakeHelpers.CreateRenderTextureForMode(mode, resolution);
                BakeHelpers.DilateMap(rt, dilateRt, backgroundColor, mask);
                RenderTexture.active = dilateRt;
            }
            else
                RenderTexture.active = rt;
            
            var bakedTexture = BakeHelpers.CreateBakingTextureForMode(mode, resolution);
            GL.sRGBWrite = !isLinear;
            
            // Read pixels from renderTexture and apply to bakedTexture
            bakedTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
      
            RenderTexture.active = null;
            if (dilateRt != null)
                RenderTexture.ReleaseTemporary(dilateRt);
            RenderTexture.ReleaseTemporary(rt);
            
            Shader.DisableKeyword(DEBUG_DISPLAY);

            if (CanBakedTextureBeIgnored(bakedTexture, mode, mask))
            {
                Object.DestroyImmediate(bakedTexture);
                return null;
            }
            
            return new TextureWithTransform(bakedTexture, offset, scale);
        }
        
        private static void DeactivateGlobalDebugProperties()
        {
            // IF URP
            
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

        private static bool CanBakedTextureBeIgnored(Texture2D bakedTexture, MaterialMode mode, Texture2D mask = null)
        {
            if (mode == MaterialMode.NormalTangentSpace)
            {
                if (BakeHelpers.TextureHasSingleValue(bakedTexture, out var normColor, mask))
                    if (BakeHelpers.ColorProximity(normColor, new Color(0.5f, 0.5f, 1f, 1f), 0.01f))
                        return true;
            }
            else if (BakeHelpers.IsTextureEmpty(bakedTexture, mask))
                return true;

            return false;
        }
        
        private static int FindForwardPassIndex(Material material)
        {
            var index = material.FindPass("Forward");
            if (index == -1)
                index = material.FindPass("ForwardLit");
            if (index == -1)
                index = material.FindPass("ForwardOnly");
            if (index == -1)
                index = material.FindPass("Universal Forward");

            if (index == -1)
            {
                var passes = "";
                for (int i = 0; i < material.passCount; i++)
                    passes += material.GetPassName(i)+"\n";

                Debug.LogWarning($"Material {material.name} does not have a Forward pass. Available passes:\n{passes}");
                return -1;
            }
            return index;
        }
        
        private static void BakeTextureSpace(Material mat, MaterialMode mode, TextureResolution resolution, out TextureWithTransform baked, Texture2D mask = null)
        {
            bool isLinear = BakeHelpers.IsDebugMaterialModeInLinear(mode);
            var material = new Material(mat);
            // HACK: disable a view-dependant effect on a particular shader
            if (material.HasFloat("_Fresnel_Normal_Overide"))
                material.SetFloat("_Fresnel_Normal_Overide", 0f);
            
            Shader.EnableKeyword(DEBUG_DISPLAY);
            Shader.SetGlobalFloat("_DebugMaterialMode", (int)mode.ToRPSpecific());
      
            DeactivateGlobalDebugProperties();
            
            var bakedTexture = BakeHelpers.CreateBakingTextureForMode(mode, resolution);
            GL.sRGBWrite = !isLinear;
            
            // Render mesh with bakeMat to bakedTexture
            var renderTexture = BakeHelpers.CreateRenderTextureForMode(mode, resolution);
            var forwardPassIndex = FindForwardPassIndex(material);
            Graphics.Blit(bakedTexture, renderTexture, material, forwardPassIndex);
           
            RenderTexture.active = renderTexture;

            // Read pixels from renderTexture and apply to bakedTexture
            bakedTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
      
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);
            Object.DestroyImmediate(material);
            
            Shader.DisableKeyword(DEBUG_DISPLAY);
            
            if (CanBakedTextureBeIgnored(bakedTexture, mode, mask))
            {
                Object.DestroyImmediate(bakedTexture);
                baked = null;
                return;
            } 
            baked = new TextureWithTransform(bakedTexture);
        }
    }
}