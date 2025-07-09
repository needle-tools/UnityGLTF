using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityGLTF
{
    public static class ShaderModifier 
    {
        enum Mode
        {
            ClipSpace,
            WorldSpace,
        }
        
        private static readonly Dictionary<Shader, (bool isAmplifyShader, bool hasDebugModeEnabled, DateTime lastChange)> _amplifyCheckCache = new Dictionary<Shader, (bool, bool, DateTime)>();
        private static readonly Dictionary<Shader, (Shader shader, DateTime lastChange)> _amplifyPatched = new Dictionary<Shader, (Shader, DateTime)>();
        
        public static bool IsAmplifyShader(Shader shader, out bool hasDebugModeEnabled)
        {
            if (_amplifyCheckCache.TryGetValue(shader, out var cacheEntry))
            {
                var currentWriteTime = System.IO.File.GetLastWriteTime(AssetDatabase.GetAssetPath(shader));
                if (cacheEntry.lastChange == currentWriteTime)
                {
                    hasDebugModeEnabled = cacheEntry.hasDebugModeEnabled;
                    return cacheEntry.isAmplifyShader;
                }
            }
            
            hasDebugModeEnabled = false;
            var sourcePath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(sourcePath))
            {
                _amplifyCheckCache.Add(shader, (false, false, DateTime.MinValue));
                return false;
            }

            var source = System.IO.File.ReadAllText(sourcePath);
            var lastWriteTime = System.IO.File.GetLastWriteTime(sourcePath); 
            if (string.IsNullOrEmpty(source))
            {
                _amplifyCheckCache.Add(shader, (false, false, DateTime.MinValue));
                return false;
            }
            
            hasDebugModeEnabled = source.Contains("#pragma multi_compile_fragment _ DEBUG_DISPLAY", StringComparison.Ordinal);
            var isASE = source.Contains("/*ASEBEGIN", StringComparison.Ordinal);

            if (_amplifyCheckCache.ContainsKey(shader))
                _amplifyCheckCache.Remove(shader);
            _amplifyCheckCache.Add(shader, (isASE, hasDebugModeEnabled, lastWriteTime));

            // if (!hasDebugModeEnabled)
            // {
            //     Debug.LogError("Amplify Shader " + shader.name + " does not have DEBUG_DISPLAY enabled. Please enable it to use this shader for backing.");
            // }
            
            return isASE;
        }

        public static bool RequiresTextureSpacePatching(Shader shader)
        {
            if (IsAmplifyShader(shader, out bool hasDebugModeEnabled))
            {
                if (shader.FindSubshaderTagValue(0, new ShaderTagId("UniversalMaterialType")).name == "Unlit")
                {
                    // For Amplify Shader, we need to patch the shader source, to add DEBUG_DISPLAY
                    return true;
                }

                if (!hasDebugModeEnabled)
                    return true;
            }

            return false;
        }

        public static Shader PatchAmplifyShaderForTextureSpace(Shader shader)
        {
            if (!RequiresTextureSpacePatching(shader))
                return shader;

            bool isUnlit = shader.FindSubshaderTagValue(0, new ShaderTagId("UniversalMaterialType")).name == "Unlit";
            
            
            var currentWriteTime = System.IO.File.GetLastWriteTime(AssetDatabase.GetAssetPath(shader));
            if (_amplifyPatched.TryGetValue(shader, out var cachedShader))
            {
                if (cachedShader.lastChange == currentWriteTime)
                {
                    return cachedShader.shader;
                }
            }
            
            var shaderSource = GetShaderSource(shader);
            if (!IsAmplifyShader(shader, out bool hasDebugModeEnabled))
            {
                Debug.LogError($"Shader {shader.name} is not a valid Amplify Shader. Cannot patch for texture space.");
                return shader;
            }

   
            
            // For Amplify Shader, we need to patch the shader source
            if (isUnlit)
                shaderSource = PatchDebugViewToAmplifyUnlitShader(shaderSource, shader);
            else
                shaderSource = PatchDebugViewToAmplifiyShader(shaderSource);
            
            var shaderAsset = ShaderUtil.CreateShaderAsset(null, shaderSource, true);
            shaderAsset.name = shader.name + "(Patched)";
            // Check for errors
            var errors = ShaderUtil.GetShaderMessages(shaderAsset);
            if (errors != null && errors.Length > 0)
            {
                foreach (var error in errors)
                {
                    if (error.severity == ShaderCompilerMessageSeverity.Warning)
                        Debug.LogWarning($"Shader {shaderAsset.name} has warning: {error}");
                    else if (error.severity == ShaderCompilerMessageSeverity.Error)
                        Debug.LogError($"Shader {shaderAsset.name} has error: {error}");
                }
                // Don't hold a invalid shader asset in memory
                Object.DestroyImmediate(shaderAsset);
                return shader;

            }
            
            if (_amplifyPatched.ContainsKey(shader))
                _amplifyPatched.Remove(shader);
            _amplifyPatched.Add(shader, (shaderAsset, currentWriteTime));
            
            Debug.Log($"<color=#808080ff>Patched Amplify Shader {shader.name}.</color>");
            
            // Write to file for debugging
            var sourcePath = AssetDatabase.GetAssetPath(shader);
            File.WriteAllText(sourcePath + "_debug.shader", shaderSource);
            
            return shaderAsset;
        }


        private static string PatchDebugViewToAmplifiyShader(string shaderSource)
        {
            var pragmaLine = "\t#pragma multi_compile_fragment _ DEBUG_DISPLAY";
            if (!shaderSource.Contains(pragmaLine, StringComparison.Ordinal))
            {
                int pragmaIndex = 0;
                while (true)
                {
                    pragmaIndex= shaderSource.IndexOf("HLSLPROGRAM", pragmaIndex, StringComparison.Ordinal);
                    if (pragmaIndex == -1)
                        break;
                    
                    shaderSource = shaderSource.Insert(pragmaIndex + 11, pragmaLine);
                    pragmaIndex += pragmaLine.Length + 11; // 11 is the length of "HLSLPROGRAM"
                }
            }
            
            string debugInclude =
                "\t\t\t#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl\"\n";
            if (shaderSource.Contains(debugInclude))
                return shaderSource;
            
            int index = 0;
            // Add include
            while (true)
            {
                index = shaderSource.IndexOf(
                    "#include \"Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl\"",
                    index, StringComparison.Ordinal);
                if (index == -1)
                    break;
                
                shaderSource = shaderSource.Insert(index - 1, debugInclude);
                index += debugInclude.Length + 10; 
            }

            return shaderSource;
        }
        
        private static string PatchDebugViewToAmplifyUnlitShader(string shaderSource, Shader shader)
        {
            string debugFunc = "\t\t\t\t#if defined(DEBUG_DISPLAY)\n\t\t\t\t    half4 debugColor;\n\t\t\t\t\tSurfaceData surfaceData = (SurfaceData)0;\n\t\t\t\t\tsurfaceData.albedo = Color;\n\t\t\t\t\tsurfaceData.alpha = Alpha;\n\t\t\t\t    if (CanDebugOverrideOutputColor(inputData, surfaceData, debugColor))\n\t\t\t\t    {\n\t\t\t\t        return debugColor;\n\t\t\t\t    }\n\t\t\t\t#endif\n";
       
            int index = 0;
            shaderSource = PatchDebugViewToAmplifiyShader(shaderSource);
 

            // Remove Alpha clipping
            index = 0;
            while (true)
            {
                index = shaderSource.IndexOf("Name \"Forward\"", index, StringComparison.Ordinal);
                if (index == -1)
                    break;
                
                index = shaderSource.IndexOf("#ifdef _ALPHATEST_ON", index, StringComparison.Ordinal);
                if (index == -1)
                    break;
                
                var endIndex = shaderSource.IndexOf("#endif", index, StringComparison.Ordinal);
                if (endIndex == -1)
                    break;
                shaderSource = shaderSource.Remove(index, endIndex - index + 6); // 6 is the length of #endif
                break;
            }
            
            // Add debug function
            index = 0;
            while (true)
            {
                index = shaderSource.IndexOf("Name \"Forward\"", index, StringComparison.Ordinal);
                if (index == -1)
                    break;
                
                index = shaderSource.IndexOf("#ifdef ASE_FOG", index, StringComparison.Ordinal);
                if (index == -1)
                    break;
                
                
                shaderSource = shaderSource.Insert(index - 1, debugFunc);
                index += debugFunc.Length + 10;
                break;
            }
            
            return shaderSource;
        }
        
        private static string PatchAmplifyShaderToClipSpace(string shaderSource, Shader shader, int uvChannel = 0)
        {
            var lastIndex = 0;
            var index = -1;
            var inserts = 0;

            if (IsAmplifyShader(shader, out bool hasDebugModeEnabled))
            {
                if (!hasDebugModeEnabled)
                    shaderSource = PatchDebugViewToAmplifiyShader(shaderSource);
            }
            
            var needsTextureCoordDefine = $"#define ASE_NEEDS_TEXTURE_COORDINATES{uvChannel}";
            
            var uvChannelName = $"texcoord{uvChannel}";
            if (uvChannel == 0)
                uvChannelName = $"texcoord";

            while (true)
            {
                var indexDefineStart = shaderSource.IndexOf("#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)", lastIndex);
                if (indexDefineStart == -1)
                    break;
                indexDefineStart = shaderSource.IndexOf("#endif", indexDefineStart, StringComparison.Ordinal);
                if (indexDefineStart == -1)
                    break;
                
                
                var indexDefineEnd = shaderSource.IndexOf("struct Attributes", indexDefineStart, StringComparison.Ordinal);
                
                var sub = shaderSource.Substring(indexDefineStart, indexDefineEnd - indexDefineStart);
                if (!sub.Contains(needsTextureCoordDefine))
                {
                    shaderSource = shaderSource.Insert(indexDefineStart+ 7, $"\n{needsTextureCoordDefine}");
                    inserts++;
                }
                lastIndex = indexDefineEnd;
            }
            
            
            var mode = Mode.WorldSpace;
            
            // Add  Pass - Conservative mode
            shaderSource = AddConservativeRasterizationPass(shaderSource);
            
            while (true)
            {
                lastIndex = index;
                index = shaderSource.IndexOf("Name \"Forward\"", lastIndex + 1, StringComparison.Ordinal);
                if (index == -1)
                    break;
                
                index = shaderSource.IndexOf("PackedVaryings VertexFunction(", lastIndex + 1, StringComparison.Ordinal);

                if (index == -1)
                    break;

                var indexOfReturn = shaderSource.IndexOf("return output;", index, StringComparison.Ordinal);

                if (indexOfReturn != -1)
                {
                    switch (mode) {
                        case Mode.ClipSpace:
                            shaderSource = shaderSource.Insert(indexOfReturn - 1,
                                "\nfloat4 p = input." + uvChannelName + ";" +
                                "p.w = 1; p.z = 0.999999; p.xy -= -1; p.z *= -1;" +
                                "output.positionCS = p;");
                            break;
                        case Mode.WorldSpace:
                            shaderSource = shaderSource.Insert(indexOfReturn - 1, $"\noutput.positionCS = TransformObjectToHClip(input.{uvChannelName});");
                            break;
                    }
                    inserts++;
                }

                index = indexOfReturn;
            }
            
            Debug.Log($"<color=#808080ff>Amplify Shader {shader.name}: found {inserts} to patch for {uvChannelName}.</color>");
            if (inserts < 1)
            {
                // For debugging, output the shader source to a file
                var sourcePath = AssetDatabase.GetAssetPath(shader);
                File.WriteAllText(sourcePath + "_debug.shader", shaderSource);
            }

            return shaderSource;
        }
        
        private static string AddConservativeRasterizationPass(string shaderSource)
        {
            if (!SystemInfo.supportsConservativeRaster)
            {
                Debug.Log($"<color=#808080ff>Conversative Rasterization is not supporting by the system. Skip enabling it in shader.</color>");
                return shaderSource;
            }
            var passString = "Name \"Forward\"";
            int addedConservativeToPasses = 0;
            int passIndex = 0;
            while (true)
            {
                passIndex = shaderSource.IndexOf(passString, passIndex + 1, StringComparison.Ordinal);
                if (passIndex == -1)
                    break;
                

                addedConservativeToPasses++;
                shaderSource = shaderSource.Insert(passIndex+passString.Length, "\n Conservative True\n");
            }
            Debug.Log($"<color=#808080ff>Shader: found {addedConservativeToPasses} passes to patch for >Conservative True<.</color>");
            return shaderSource;
        }
        
        private static string PatchShaderGraphShaderToClipSpace(string shaderSource, Shader shader, int uvChannel = 0)
        {
            var lastIndex = 0;
            var index = -1;
            var inserts = 0;
            
            var uvChannelName = $"texCoord{uvChannel}";
            
            var mode = Mode.WorldSpace;
            
            // Add  Pass - Conservative mode
            shaderSource = AddConservativeRasterizationPass(shaderSource);
            
            while (true)
            {
                lastIndex = index;
                index = shaderSource.IndexOf("PackedVaryings PackVaryings (Varyings input)", lastIndex + 1, StringComparison.Ordinal);

                if (index == -1)
                    break;

                var indexOfReturn = shaderSource.IndexOf("return output;", index, StringComparison.Ordinal);

                if (indexOfReturn != -1)
                {
                    var foundTexCoord = shaderSource.IndexOf(uvChannelName, index, StringComparison.Ordinal);

                    if (foundTexCoord == -1 || foundTexCoord > indexOfReturn)
                    {
                        // TexCoord to PackedVaryings Struct
                        var shaderSourceUntilReturn = shaderSource.Substring(0, indexOfReturn);
                        
                        var structIndex = shaderSourceUntilReturn.LastIndexOf("struct PackedVaryings", shaderSourceUntilReturn.Length-1, StringComparison.Ordinal);
                        if (structIndex == -1)
                            continue;
                       
                        var structEndIndex = shaderSourceUntilReturn.IndexOf("}", structIndex, StringComparison.Ordinal); 
                        var structLength = structEndIndex - structIndex;
                        var structBlock = shaderSource.Substring(structIndex, structLength);
                        
                        bool structHasTex = structBlock.IndexOf($"float4 {uvChannelName}", StringComparison.Ordinal) != -1;
                   
                        if (!structHasTex)
                        {
                            int lastInterpolate = 0;
                            int interpolateIndex = 0;
                            do
                            {
                                interpolateIndex = structBlock.IndexOf(": INTERP", interpolateIndex, StringComparison.Ordinal);
                                if (interpolateIndex == -1)
                                    break;
                                int interpLength = ": INTERP".Length;
                                int lineEndIndex = structBlock.IndexOf(";", interpolateIndex, StringComparison.Ordinal);
                                int interpNumberLength = lineEndIndex - interpolateIndex - interpLength;
                                var interopNumber = structBlock.Substring(interpolateIndex + interpLength, interpNumberLength);
                                var interopInt = int.Parse(interopNumber);
                                lastInterpolate = Math.Max(lastInterpolate, interopInt);
                                interpolateIndex = lineEndIndex + 1;
                                
                            } while (interpolateIndex != -1);
                            
                            shaderSource = shaderSource.Insert(structIndex + structLength - 1,
                                $"\nfloat4 {uvChannelName} : INTERP{lastInterpolate+1};");
                            foundTexCoord = structIndex + structLength - 1;
                            inserts++;
                        }
                    }

                    if (foundTexCoord != -1 && foundTexCoord < indexOfReturn)
                    {
                        indexOfReturn = shaderSource.IndexOf("return output;", index, StringComparison.Ordinal);

                        switch (mode) {
                            case Mode.ClipSpace:
                                shaderSource = shaderSource.Insert(indexOfReturn - 1,
                                    "\nfloat4 p = output." + uvChannelName + ";" +
                                    "p.w = 1; p.z = 0.999999; p.xy -= -1; p.z *= -1;" +
                                    "output.positionCS = p;");
                                break;
                            case Mode.WorldSpace:
                                shaderSource = shaderSource.Insert(indexOfReturn - 1, $"\noutput.positionCS = TransformObjectToHClip(output.{uvChannelName});");
                                break;
                        }
                        inserts++;
                    }
                }

                index = indexOfReturn;
            }
            
            Debug.Log($"<color=#808080ff>Shader {shader.name}: found {inserts} to patch for {uvChannelName}.</color>");
            if (inserts < 1)
            {
                // For debugging, output the shader source to a file
                var sourcePath = AssetDatabase.GetAssetPath(shader);
                File.WriteAllText(sourcePath + "_debug.shader", shaderSource);
            }

            return shaderSource;
        }
        
        public static Shader PatchShaderUVsToClipSpace(Shader shader, int uvChannel = 0)
        {
            var shaderSource = GetShaderSource(shader);

            if (IsShaderGraph(shader))
            {
                shaderSource = PatchShaderGraphShaderToClipSpace(shaderSource, shader, uvChannel);
            }
            else
            if (IsAmplifyShader(shader, out bool hasDebugModeEnabled))
            {
                // For Amplify Shader, we need to patch the shader source
                shaderSource = PatchAmplifyShaderToClipSpace(shaderSource, shader, uvChannel);
            }
            else
            {
                Debug.LogError($"Shader {shader.name} is not a valid Amplify Shader or Shader Graph. Cannot patch UVs to clip space.");
                return shader;
            }
            
            var shaderAsset = ShaderUtil.CreateShaderAsset(null, shaderSource, true);
            shaderAsset.name = shader.name +  $"(Patched UV{uvChannel}";
            // Check for errors
            var errors = ShaderUtil.GetShaderMessages(shaderAsset);
            if (errors != null && errors.Length > 0)
            {
                foreach (var error in errors)
                {
                    if (error.severity == ShaderCompilerMessageSeverity.Warning)
                        Debug.LogWarning($"Shader {shaderAsset.name} has warning: {error}");
                    else if (error.severity == ShaderCompilerMessageSeverity.Error)
                        Debug.LogError($"Shader {shaderAsset.name} has error: {error}");
                }
                
                // Don't hold a invalid shader asset in memory
                Object.DestroyImmediate(shaderAsset);
                return shader;
            }
            
            return shaderAsset;
        }
        
        public static bool IsShaderGraph(Shader shader)
        {
            return shader.IsShaderGraphAsset();
        }
        
        public static string GetShaderSource(Shader shader)
        {
            var shaderPath = UnityEditor.AssetDatabase.GetAssetPath(shader);

            if (IsShaderGraph(shader))
            {
                var assetCollection = new AssetCollection();
                
                // access private method "ShaderGraphImporter.GatherDependenciesFromSourceFile" by reflection
                var shaderGraphImporterType = typeof(ShaderGraphImporter);
                var gatherDependenciesMethod = shaderGraphImporterType.GetMethod("GatherDependenciesFromSourceFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (gatherDependenciesMethod == null)
                {
                    Debug.LogError("GatherDependenciesFromSourceFile method not found");
                    return null;
                }
                
                // call the method
                var parameters = new object[] { shaderPath };
                var dep = (string[]) gatherDependenciesMethod.Invoke(null, parameters);
                foreach (var d in dep)
                {
                    var assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(d);
                    if (assetType == typeof(SubGraphAsset)) 
                        assetCollection.AddAssetDependency(UnityEditor.AssetDatabase.GUIDFromAssetPath(d),AssetCollection.Flags.IsSubGraph );
                }

                return ShaderGraphImporter.GetShaderText(shaderPath, out _, assetCollection, out _);
            }
            else
            {
                return System.IO.File.ReadAllText(shaderPath);
            }
        }
        
    }
}