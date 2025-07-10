using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GLTF.Schema;
using PlasticGui.WorkspaceWindow.Items;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Task = System.Threading.Tasks.Task;

namespace UnityGLTF
{
    internal static class ChannelExporter
    {
        [MenuItem("CONTEXT/MeshRenderer/UnityGLTF/Bake in UV0 space")]
        private static void ExportChannelsUV0Space(MenuCommand command)
        {
            var renderer = command.context as Renderer;
            if (!renderer) return;
            
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var maps = MaterialBaker.BakePBRMaterial(renderer, i, new TextureResolution(2048, 2048));
                maps.forMaterial = materials[i];
                SaveMaps(maps, 0, false);
            }
        }
        
        [MenuItem("CONTEXT/MeshRenderer/UnityGLTF/Bake in UV1 space")]
        private static void ExportChannelsUV1Space(MenuCommand command)
        {
            var renderer = command.context as Renderer;
            if (!renderer) return;
            
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var maps = MaterialBaker.BakePBRMaterial(renderer, i, new TextureResolution(2048, 2048), 1);
                SaveMaps(maps, 1, false);
            }
        }
        
        [MenuItem("CONTEXT/MeshRenderer/UnityGLTF/Bake in texture space")]
        private static void ExportChannels(MenuCommand command)
        {
            var renderer = command.context as Renderer;
            if (!renderer) return;

            var materials = renderer.sharedMaterials;
            foreach (var material in materials)
            {
                var maps = MaterialBaker.BakePBRMaterial(material, new TextureResolution(2048, 2048));
                SaveMaps(maps);
            }
        }

        public static Material SaveMaps(PbrMaps maps, int uvChannel = 0, bool useTextureSpace = true)
        {
            var material = maps.forMaterial;
            var mesh = maps.forMesh;

            var textureSize = maps.GetTextureSize();

            var path = AssetDatabase.GetAssetPath(material);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                Debug.LogError("No directory found for the material.");
                return null;
            }

            var targetDirectory = "";
            var directoryForMaterial = Path.Combine(directory, fileName);
            if (!Directory.Exists(directoryForMaterial))
                Directory.CreateDirectory(directoryForMaterial);

            if (mesh)
            {
                var meshPath = AssetDatabase.GetAssetPath(mesh);
                var meshGuid = AssetDatabase.AssetPathToGUID(meshPath);
                var meshName = $"{mesh.name}-{meshGuid}";
                var directoryForMesh = Path.Combine(directoryForMaterial, meshName);
                if (!Directory.Exists(directoryForMesh))
                    Directory.CreateDirectory(directoryForMesh);

                targetDirectory = directoryForMesh;
            }
            else
            {
                targetDirectory = directoryForMaterial;
            }

            var baseColorPath = Path.Combine(targetDirectory, fileName + "_baseColor.png");
            var normalPath = Path.Combine(targetDirectory, fileName + "_normal.png");
            var emissionPath = Path.Combine(targetDirectory, fileName + "_emission.png");
            var ormPath = Path.Combine(targetDirectory, fileName + "_orm.png");
            var materialPath = Path.Combine(targetDirectory, fileName + ".mat");
            
            bool hasBaseColor = false;
            bool hasNormal = false;
            bool hasOrm = false;
            bool hasEmission = false;

            Color baseColor = Color.white;
            Color emissionColor = Color.black;
            float metallicFactor = 0f;
            float roughnessFactor = 1f;
            var encodeToPng = new List<(string path, Texture2D texture)>();
            
            EditorUtility.DisplayProgressBar("Material: "+maps.forMaterial.name, "Combining texture maps", 0.5f);
            
            if (maps.HasMap(MaterialMode.Albedo) || maps.HasMap(MaterialMode.Alpha))
            {
                var mergedAlbedoAndAlpha = new Texture2D(textureSize.width, textureSize.height, TextureFormat.RGBA32, false);
                mergedAlbedoAndAlpha.name = "Merged Albedo and Alpha";

                BurstMethods.AlbedoAlphaCombine(mergedAlbedoAndAlpha, maps.albedo?.map, maps.alpha?.map);

                if (BakeHelpers.TextureHasSingleValue(mergedAlbedoAndAlpha, out var singleBaseColorTex, maps.mask?.map))
                {
                    baseColor = singleBaseColorTex;
                    Object.DestroyImmediate(mergedAlbedoAndAlpha);
                }
                else
                {
                    encodeToPng.Add(new (baseColorPath, mergedAlbedoAndAlpha));
                    hasBaseColor = true;
                }
            }

            bool metallicHasSingleValue = true;
            bool smoothnessHasSingleValue = true;
            bool occlusionHasSingleValue = true;

            bool metallicSingleValueOrEmpty = true;
            bool smoothnessSingleValueOrEmpty = true;
            bool occlusionSingleValueOrEmpty = true;

            metallicFactor = 0f;
            roughnessFactor = 1f;
            
            if (maps.HasMap(MaterialMode.Metallic) || 
                maps.HasMap(MaterialMode.Smoothness) ||
                maps.HasMap(MaterialMode.AmbientOcclusion))
            {
                metallicHasSingleValue = maps.HasMapSingleColor(MaterialMode.Metallic, out var metallicColorTex);
                smoothnessHasSingleValue = maps.HasMapSingleColor(MaterialMode.Smoothness, out var smoothnessColorTex);
                occlusionHasSingleValue = maps.HasMapSingleColor(MaterialMode.AmbientOcclusion, out var occlusionColorTex);

                metallicSingleValueOrEmpty = !maps.HasMap(MaterialMode.Metallic) || metallicHasSingleValue;
                smoothnessSingleValueOrEmpty = !maps.HasMap(MaterialMode.Smoothness) || smoothnessHasSingleValue;
                occlusionSingleValueOrEmpty = !maps.HasMap(MaterialMode.AmbientOcclusion) ||
                                              (occlusionHasSingleValue && occlusionColorTex == Color.white);
                
                bool createOrmTexture = !occlusionSingleValueOrEmpty || !metallicSingleValueOrEmpty || !smoothnessSingleValueOrEmpty;
                
                if (createOrmTexture)
                {
                    var orm = CreateORMTexture(maps, out metallicFactor, out roughnessFactor);
                    encodeToPng.Add(new (ormPath, orm));
                    hasOrm = true;
                }
                else
                {
                    if (smoothnessHasSingleValue)
                        roughnessFactor = (1f - smoothnessColorTex.r);
                    if (metallicHasSingleValue)
                        metallicFactor = metallicColorTex.r;
                }
            }

            if (maps.HasMap(MaterialMode.NormalTangentSpace))
            {
                encodeToPng.Add(new (normalPath, maps.normal.map));
                hasNormal = true;
            }

            if (maps.HasMap(MaterialMode.Emission))
            {
                if (maps.HasMapSingleColor(MaterialMode.Emission, out var emissionColorTex))
                    emissionColor = emissionColorTex;
                else
                {
                    BurstMethods.ConvertEmissionPixels(maps.emission.map, out emissionColor);
                    encodeToPng.Add(new (emissionPath, maps.emission.map));
                    hasEmission = true;
                }
            }
            EditorUtility.ClearProgressBar();
            
            // Show Progress Bar
            EditorUtility.DisplayProgressBar("Material: "+maps.forMaterial.name, "Encoding textures to PNG files", 0.5f);
            EncodePNGToFile(encodeToPng);
            EditorUtility.ClearProgressBar();
            
            EditorUtility.DisplayProgressBar("Material: "+maps.forMaterial.name, "Importing textures", 0.5f);
            
            AssetDatabase.StartAssetEditing();
            foreach (var png in encodeToPng)
                AssetDatabase.ImportAsset(png.path, ImportAssetOptions.ForceUncompressedImport);
            AssetDatabase.StopAssetEditing();

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayProgressBar("Material: "+maps.forMaterial.name, "Create baked Material", 0.5f);
            
            // set the import settings for these textures. baseColor and emission are sRGB, normal is normal map, orm is linear
            var newMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            GetMaterialSettings(maps, out var unlit, out var alphaMode, out var alphaCutoff, out var doubleSided);

            Shader usingGltfShader = Shader.Find(unlit ? "UnityGLTF/UnlitGraph" : "UnityGLTF/PBRGraph");
            if (!newMaterial)
                newMaterial = new Material(usingGltfShader);
            else if (newMaterial.shader != usingGltfShader) 
                newMaterial.shader = usingGltfShader;

            int maxTextureSize = Mathf.Max(textureSize.width, textureSize.height);
            
            if (hasBaseColor)
            {
                var baseColorImporter = AssetImporter.GetAtPath(baseColorPath) as TextureImporter;
                baseColorImporter.textureType = TextureImporterType.Default;
                baseColorImporter.SaveAndReimport();
                baseColorImporter.maxTextureSize = maxTextureSize;

                baseColorImporter.textureCompression = TextureImporterCompression.Uncompressed;
                var importedBaseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
                newMaterial.SetTexture("baseColorTexture", importedBaseColor);
            }
            else
                newMaterial.SetTexture("baseColorTexture", null);
            
            bool anyTextureTransform = false;
            if (!unlit)
            {
                if (hasNormal)
                {
                    var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    normalImporter.maxTextureSize = maxTextureSize;
                    normalImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    normalImporter.SaveAndReimport();
                    var importedNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                    newMaterial.SetTexture("normalTexture", importedNormal);
                }
                else
                    newMaterial.SetTexture("normalTexture", null);

                if (hasEmission)
                {
                    var emissionImporter = AssetImporter.GetAtPath(emissionPath) as TextureImporter;
                    emissionImporter.textureType = TextureImporterType.Default;
                    emissionImporter.maxTextureSize = maxTextureSize;
                    emissionImporter.sRGBTexture = true;
                    emissionImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    emissionImporter.SaveAndReimport();
                    var importedEmission = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath);
                    newMaterial.SetTexture("emissiveTexture", importedEmission);
                }
                else
                    newMaterial.SetTexture("emissiveTexture", null);

                if (hasOrm)
                {
                    var ormImporter = AssetImporter.GetAtPath(ormPath) as TextureImporter;
                    ormImporter.textureType = TextureImporterType.Default;
                    ormImporter.sRGBTexture = false;
                    ormImporter.maxTextureSize = maxTextureSize;
                    ormImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    ormImporter.SaveAndReimport();
                    var importedOrm = AssetDatabase.LoadAssetAtPath<Texture2D>(ormPath);
                    if (!metallicSingleValueOrEmpty || !smoothnessSingleValueOrEmpty)
                        newMaterial.SetTexture("metallicRoughnessTexture", importedOrm);
                    if (!occlusionSingleValueOrEmpty)
                        newMaterial.SetTexture("occlusionTexture", importedOrm);
                }
                else
                {
                    newMaterial.SetTexture("metallicRoughnessTexture", null);
                    newMaterial.SetTexture("occlusionTexture", null);

                }
                
                var mapper = new PBRGraphMap(newMaterial);

                // Ensure multiplicative defaults#
                mapper.MetallicFactor = metallicFactor;
                mapper.RoughnessFactor = roughnessFactor;
                mapper.EmissiveFactor = emissionColor;
                mapper.OcclusionTexStrength = 1f;
                mapper.BaseColorFactor = baseColor;
                mapper.NormalTexScale = 1;
                // Set desired UV channels – based on the space we baked into
                mapper.BaseColorTexCoord = uvChannel;
                mapper.NormalTexCoord = uvChannel;
                mapper.EmissiveTexCoord = uvChannel;
                mapper.OcclusionTexCoord = uvChannel;
                mapper.MetallicRoughnessTexCoord = uvChannel;

                mapper.DoubleSided = doubleSided;
                mapper.AlphaMode = alphaMode;
                mapper.AlphaCutoff = alphaCutoff;
                
                if (hasOrm)
                {
                    if (maps.metallic != null && !maps.metallic.hasDefaultTransform)
                    {
                        mapper.MetallicRoughnessXOffset = maps.metallic.offset;
                        mapper.MetallicRoughnessXScale = maps.metallic.scale;
                    }
                    else if (maps.smoothness != null && !maps.smoothness.hasDefaultTransform)
                    {
                        mapper.MetallicRoughnessXOffset = maps.smoothness.offset;
                        mapper.MetallicRoughnessXScale = maps.smoothness.scale;
                    }
                    else if (maps.occlusion != null && !maps.occlusion.hasDefaultTransform)
                    {
                        mapper.MetallicRoughnessXOffset = maps.occlusion.offset;
                        mapper.MetallicRoughnessXScale = maps.occlusion.scale;
                    }
                    else
                    {
                        mapper.MetallicRoughnessXOffset = Vector2.zero;
                        mapper.MetallicRoughnessXScale = Vector2.one;
                    }

                    anyTextureTransform = true;
                }

                if (hasNormal && !maps.normal.hasDefaultTransform)
                {
                    mapper.NormalXOffset = maps.normal.offset;
                    mapper.NormalXScale = maps.normal.scale;
                    anyTextureTransform = true;
                }
                else
                {
                    mapper.NormalXOffset = Vector2.zero;
                    mapper.NormalXScale = Vector2.one;
                }

                if (hasEmission && !maps.emission.hasDefaultTransform)
                {
                    mapper.EmissiveXOffset = maps.emission.offset;
                    mapper.EmissiveXScale = maps.emission.scale;
                    anyTextureTransform = true;
                }
                else
                {
                    mapper.EmissiveXOffset = Vector2.zero;
                    mapper.EmissiveXScale = Vector2.one;
                }

                if (hasOrm && maps.occlusion != null && !maps.occlusion.hasDefaultTransform)
                {
                    mapper.OcclusionXOffset = maps.occlusion.offset;
                    mapper.OcclusionXScale = maps.occlusion.scale;
                    anyTextureTransform = true;
                }
                else
                {
                    mapper.OcclusionXOffset = Vector2.zero;
                    mapper.OcclusionXScale = Vector2.one;
                }

                if (hasBaseColor && maps.albedo != null && !maps.albedo.hasDefaultTransform)
                {
                    mapper.BaseColorXOffset = maps.albedo.offset;
                    mapper.BaseColorXScale = maps.albedo.scale;
                    anyTextureTransform = true;
                }
                else if (hasBaseColor && maps.alpha != null && !maps.alpha.hasDefaultTransform)
                {
                    mapper.BaseColorXOffset = maps.alpha.offset;
                    mapper.BaseColorXScale = maps.alpha.scale;
                    anyTextureTransform = true;
                }
                else
                {
                    mapper.BaseColorXOffset = Vector2.zero;
                    mapper.BaseColorXScale = Vector2.one;
                }
            }
            else
            { // Unlit

                var unlitMapper = new UnlitGraphMap(newMaterial);
                unlitMapper.BaseColorFactor = baseColor;
                unlitMapper.BaseColorTexCoord = uvChannel;
                unlitMapper.DoubleSided = doubleSided;
                unlitMapper.AlphaMode = alphaMode;
                unlitMapper.AlphaCutoff = alphaCutoff;
                
                if (hasBaseColor && maps.albedo != null && !maps.albedo.hasDefaultTransform)
                {
                    unlitMapper.BaseColorXOffset = maps.albedo.offset;
                    unlitMapper.BaseColorXScale = maps.albedo.scale;
                    anyTextureTransform = true;
                    
                }
                else if (hasBaseColor && maps.alpha != null && !maps.alpha.hasDefaultTransform)
                {
                    unlitMapper.BaseColorXOffset = maps.alpha.offset;
                    unlitMapper.BaseColorXScale = maps.alpha.scale;
                    anyTextureTransform = true;
                }
                else
                {
                    unlitMapper.BaseColorXOffset = Vector2.zero;
                    unlitMapper.BaseColorXScale = Vector2.one;
                }
            }

            GLTFMaterialHelper.SetKeyword(newMaterial, "_TEXTURE_TRANSFORM", anyTextureTransform);
            
            if (!AssetDatabase.Contains(newMaterial))
                AssetDatabase.CreateAsset(newMaterial, materialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.ClearProgressBar();

            return newMaterial;
        }

        private static Texture2D CreateORMTexture(PbrMaps maps, out float metallicFactor, out float roughnessFactor)
        {
            metallicFactor = 0f;
            roughnessFactor = 1f;
            var textureSize = maps.GetTextureSize();
            var orm = new Texture2D( textureSize.width, textureSize.height,  TextureFormat.RGB24, false, true);
            orm.name = "Occlusion Roughness Metallic";

            var metallicHasSingleValue = maps.HasMapSingleColor(MaterialMode.Metallic, out var metallicColorTex);
            var smoothnessHasSingleValue = maps.HasMapSingleColor(MaterialMode.Smoothness, out var smoothnessColorTex);
            var occlusionHasSingleValue = maps.HasMapSingleColor(MaterialMode.AmbientOcclusion, out var occlusionColorTex);

            var metallicSingleValueOrEmpty = !maps.HasMap(MaterialMode.Metallic) || metallicHasSingleValue;
            var smoothnessSingleValueOrEmpty = !maps.HasMap(MaterialMode.Smoothness) || smoothnessHasSingleValue;
            var occlusionSingleValueOrEmpty = !maps.HasMap(MaterialMode.AmbientOcclusion) ||
                                          (occlusionHasSingleValue && occlusionColorTex == Color.white);
            var ormPixels = orm.GetPixelData<Color24RGB>(0);
            var metPixels = maps.metallic?.map?.GetPixelData<Color24RGB>(0);
            var smoothPixels = maps.smoothness?.map?.GetPixelData<Color24RGB>(0);
            var occlPixels = maps.occlusion?.map?.GetPixelData<Color24RGB>(0);
            var pixelsLength = metPixels?.Length ?? smoothPixels?.Length ?? occlPixels?.Length ?? 0;
            
            var ormJob = new BurstMethods.OrmCombineJob
            {
                occlusionSingleValueOrEmpty = occlusionSingleValueOrEmpty,
                metallicSingleValueOrEmpty = metallicSingleValueOrEmpty,
                smoothnessSingleValueOrEmpty = smoothnessSingleValueOrEmpty,
                occlusion = occlPixels.HasValue ? occlPixels.Value : default,
                metallic = metPixels.HasValue ? metPixels.Value : default,
                smoothness = smoothPixels.HasValue ? smoothPixels.Value : default,
                orm = ormPixels,
                length = pixelsLength,
                parallelCount = 4
            };
            ormJob.CreateRanges();
            ormJob.Run(ormJob.parallelCount);
            ormJob.Dispose();
            
            if (metallicHasSingleValue)
                metallicFactor = metallicColorTex.r;
            else if (maps.metallic != null)
                metallicFactor = 1f;
                    
            if (smoothnessHasSingleValue)
                roughnessFactor = (1f - smoothnessColorTex.r);
            else if (maps.smoothness != null)
                roughnessFactor = 1f;

            return orm;
        }

        private static void EncodePNGToFile(List<(string path, Texture2D texture)> encodeToPng)
        {
            var tasks = new List<System.Threading.Tasks.Task>();
            foreach (var pngEncode in encodeToPng)
            {
                var gf = pngEncode.texture.graphicsFormat;
                var bytes = pngEncode.texture.GetRawTextureData<byte>();
                var width = pngEncode.texture.width;
                var height = pngEncode.texture.height;

                var newTask = Task.Run(() =>
                {
                    var pngBytes = ImageConversion.EncodeNativeArrayToPNG(bytes, gf, (uint)width, (uint)height);
                    File.WriteAllBytes(pngEncode.path, pngBytes.ToArray());
                    pngBytes.Dispose();
                });
                tasks.Add(newTask);
            }
            Task.WhenAll(tasks).Wait();
        }

        private static void GetMaterialSettings(PbrMaps maps, out bool isUnlit, out AlphaMode alphaMode, out float alphaCutoff,
            out bool doubleSided)
        {
            isUnlit = false;

            var matType = maps.forMaterial.GetTag("UniversalMaterialType", false);
            if (matType == "Unlit" || matType == "UnlitShaderGraph")
            {
                isUnlit = true;
            }
            else if (maps.forMaterial.shader.name.Contains("unlit", StringComparison.OrdinalIgnoreCase))
            {
                isUnlit = true;
            }
        
            
            alphaMode = AlphaMode.OPAQUE;
            alphaCutoff = 0.5f;
            doubleSided = false;
            var isBirp = !GraphicsSettings.currentRenderPipeline;
            switch (maps.forMaterial.GetTag("RenderType", false, ""))
            {
                case "TransparentCutout":
                    if (maps.forMaterial.HasProperty("alphaCutoff"))
                        alphaCutoff = maps.forMaterial.GetFloat("alphaCutoff");
                    else if (maps.forMaterial.HasProperty("_AlphaCutoff"))
                        alphaCutoff = maps.forMaterial.GetFloat("_AlphaCutoff");
                    else if (maps.forMaterial.HasProperty("_Cutoff"))
                        alphaCutoff = maps.forMaterial.GetFloat("_Cutoff");
                    alphaMode = AlphaMode.MASK;
                    break;
                case "Transparent":
                case "Fade":
                    alphaMode = AlphaMode.BLEND;
                    break;
                default:
                    if ((!isBirp && maps.forMaterial.IsKeywordEnabled("_ALPHATEST_ON")) ||
                        (isBirp && maps.forMaterial.IsKeywordEnabled("_BUILTIN_ALPHATEST_ON")) ||
                        maps.forMaterial.renderQueue == 2450)
                    {
                        if (maps.forMaterial.HasProperty("alphaCutoff"))
                            alphaCutoff = maps.forMaterial.GetFloat("alphaCutoff");
                        else if (maps.forMaterial.HasProperty("_AlphaCutoff"))
                            alphaCutoff = maps.forMaterial.GetFloat("_AlphaCutoff");
                        else if (maps.forMaterial.HasProperty("_Cutoff"))
                            alphaCutoff = maps.forMaterial.GetFloat("_Cutoff");
                        else if (maps.forMaterial.HasProperty("Alpha Cutoff"))
                            alphaCutoff = maps.forMaterial.GetFloat("Alpha Cutoff");
                            
                        alphaMode = AlphaMode.MASK;
                    }
                    else
                    {
                        alphaMode = AlphaMode.OPAQUE;
                    }
                    break;
            }

            doubleSided = (maps.forMaterial.HasProperty("_Cull") && maps.forMaterial.GetInt("_Cull") == (int)CullMode.Off) ||
                          (maps.forMaterial.HasProperty("_CullMode") && maps.forMaterial.GetInt("_CullMode") == (int)CullMode.Off) ||
                          (maps.forMaterial.shader.name.EndsWith("-Double")); // workaround for exporting shaders that are set to double-sided on 2020.3
     
            // We don't get Cull Mode from Amplify Shader from the Properties, so we check the shader source 
            if (ShaderModifier.IsAmplifyShader(maps.forMaterial.shader, out _))
            {
                var shaderSource = ShaderModifier.GetShaderSource(maps.forMaterial.shader);
                if (shaderSource != null)
                {
                    var indexForwadPass = shaderSource.IndexOf("Name \"Forward\"", StringComparison.Ordinal);
                    var indexCullOff = shaderSource.IndexOf("Cull Off", indexForwadPass, StringComparison.Ordinal);
                    var indexHSLSForwardPass = shaderSource.IndexOf("HLSLPROGRAM", indexForwadPass, StringComparison.Ordinal);
                    if (indexCullOff != -1 && indexCullOff < indexHSLSForwardPass)
                    {
                        doubleSided = true;
                    }
                    else
                    {
                        var indexSubShader = shaderSource.IndexOf("SubShader", StringComparison.Ordinal);
                        var indexHSLSInclude = shaderSource.IndexOf("HLSLINCLUDE", indexSubShader, StringComparison.Ordinal);
                        indexCullOff = shaderSource.IndexOf("Cull Off", indexSubShader, StringComparison.Ordinal);
                        if (indexCullOff != -1 && indexCullOff < indexHSLSInclude)
                            doubleSided = true;

                    }
                    

                    if (shaderSource.Contains("#pragma multi_compile_local _ALPHATEST_ON"))
                    {
                        alphaMode = AlphaMode.MASK;
                    }
                }
                
            }
        }

        [MenuItem("CONTEXT/MeshRenderer/UnityGLTF/Switch to converted material")]
        private static void SwitchToConvertedMaterial(MenuCommand command)
        {
            var renderer = command.context as Renderer;
            if (!renderer) return;

            var materials = renderer.sharedMaterials;
            var newMaterials = new Material[materials.Length];
            Array.Copy(materials, newMaterials, materials.Length);

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];

                var path = AssetDatabase.GetAssetPath(material);
                var fileName = Path.GetFileNameWithoutExtension(path);
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    Debug.LogWarning("No directory found for the material.");
                    continue;
                }

                var newDirectory = Path.Combine(directory, fileName);
                if (!Directory.Exists(newDirectory))
                {
                    Debug.LogWarning("No directory found for the material.");
                    continue;
                }

                var newMaterialPath = Path.Combine(newDirectory, fileName + ".mat");
                var newMaterial = AssetDatabase.LoadAssetAtPath<Material>(newMaterialPath);
                if (!newMaterial)
                {
                    Debug.LogWarning("No material found at " + newMaterialPath);
                    continue;
                }

                newMaterials[i] = newMaterial;
            }

            Undo.RegisterCompleteObjectUndo(renderer, "Switch to converted material");
            renderer.sharedMaterials = newMaterials;
        }

        [MenuItem("CONTEXT/MeshRenderer/UnityGLTF/Switch to original material")]
        private static void SwitchToOriginalMaterial(MenuCommand command)
        {
            var renderer = command.context as Renderer;
            if (!renderer) return;

            var materials = renderer.sharedMaterials;
            var newMaterials = new Material[materials.Length];
            Array.Copy(materials, newMaterials, materials.Length);

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];

                var path = AssetDatabase.GetAssetPath(material);
                // one directory up, same file name
                var fileName = Path.GetFileNameWithoutExtension(path);
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    Debug.LogWarning("No directory found for the material.");
                    continue;
                }

                var newDirectory = Path.GetDirectoryName(directory);
                ;
                if (!Directory.Exists(newDirectory))
                {
                    Debug.LogWarning("No directory found for the material.");
                    continue;
                }

                var newMaterialPath = Path.Combine(newDirectory, fileName + ".mat");
                var newMaterial = AssetDatabase.LoadAssetAtPath<Material>(newMaterialPath);
                if (!newMaterial)
                {
                    Debug.LogWarning("No material found at " + newMaterialPath);
                    continue;
                }
                
                newMaterials[i] = newMaterial;
            }

            Undo.RegisterCompleteObjectUndo(renderer, "Switch to original material");
            renderer.sharedMaterials = newMaterials;
        }
    }
}