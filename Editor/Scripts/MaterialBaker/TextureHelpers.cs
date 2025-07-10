using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityGLTF
{
    public static class BakeHelpers
    {
        private static Material _dilateMaterial;

        public static bool IsDebugMaterialModeInLinear(MaterialMode mode)
        {
            bool isLinear = false;
            switch (mode)
            {
                case MaterialMode.Emission:
                case MaterialMode.Alpha:
                case MaterialMode.Smoothness:
                case MaterialMode.AmbientOcclusion:
                case MaterialMode.NormalWorldSpace:
                case MaterialMode.NormalTangentSpace:
                case MaterialMode.Metallic:
                case MaterialMode.SpriteMask:
                    isLinear = true;
                    break;
            }

            return isLinear;
        }

        public static bool HasAlpha(MaterialMode mode)
        {
            switch (mode)
            {
                case MaterialMode.Albedo:
                case MaterialMode.Alpha:
                    return true;
                case MaterialMode.SpriteMask:
                case MaterialMode.Specular:
                case MaterialMode.Smoothness:
                case MaterialMode.AmbientOcclusion:
                case MaterialMode.Emission:
                case MaterialMode.NormalWorldSpace:
                case MaterialMode.NormalTangentSpace:
                case MaterialMode.Metallic:
                    return false;
                default:
                    return true;
            }
        }
        
        public static bool ColorProximity(Color a, Color b, float threshold = 0.001f)
        {
            return Mathf.Abs(a.r - b.r) < threshold &&
                   Mathf.Abs(a.g - b.g) < threshold &&
                   Mathf.Abs(a.b - b.b) < threshold &&
                   Mathf.Abs(a.a - b.a) < threshold;
        }
        
        public static unsafe bool TextureHasSingleValue(Texture2D texture, out Color singleValue, Texture2D mask = null)
        {
            bool hasSingleValue = false;
            if (texture.format == TextureFormat.RGBA32)
            {
                var pd = texture.GetPixelData<Color32RGBA>(0);
                BurstMethods.HasSingleValueRGBA((Color32RGBA*)texture.GetPixelData<Color32RGBA>(0).GetUnsafeReadOnlyPtr(),
                    mask ? (Color24RGB*)mask.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null,
                    texture.GetPixelData<Color32RGBA>(0).Length, out hasSingleValue, out Color32RGBA singleColor);

                singleValue = singleColor.ToColor();
            }
            else if (texture.format == TextureFormat.RGB24)
            {
                var pd = texture.GetPixelData<Color24RGB>(0);
                BurstMethods.HasSingleValueRGB((Color24RGB*)texture.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr(),
                    mask ? (Color24RGB*)mask.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null,
                    texture.GetPixelData<Color24RGB>(0).Length, out hasSingleValue, out Color24RGB singleColor);

                singleValue = singleColor.ToColor();
            }
            else if (texture.format == TextureFormat.RGBAFloat)
            {
                var pd = texture.GetPixelData<float4>(0);
                BurstMethods.HasSingleValueRGBAFloat((float4*)texture.GetPixelData<float4>(0).GetUnsafeReadOnlyPtr(),
                    mask ? (Color24RGB*)mask.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null,
                    texture.GetPixelData<float4>(0).Length, out hasSingleValue, out float4 singleColor);

                singleValue = new Color(singleColor.x, singleColor.y, singleColor.z, singleColor.w);
            }
            else
            {
                Debug.LogError("Unsupported texture format for single value check: " + texture.format);
                singleValue = Color.clear;
                return false;
            }
            return hasSingleValue;
        }
        
        public static unsafe bool IsTextureEmpty(Texture2D texture, bool ignoreAlpha = true, Texture2D mask = null)
        {
            bool isEmpty = false;
            if (texture.format == TextureFormat.RGBA32)
            {
                var pd = texture.GetPixelData<Color32RGBA>(0);
                BurstMethods.IsTextureEmptyRGBA((Color32RGBA*)texture.GetPixelData<Color32RGBA>(0).GetUnsafeReadOnlyPtr(),
                    mask ? (Color24RGB*)mask.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null, 
                    texture.GetPixelData<Color32RGBA>(0).Length, 
                    ignoreAlpha, 
                    out isEmpty );
            }
            else
            if (texture.format == TextureFormat.RGB24)
            {
                var pd = texture.GetPixelData<Color24RGB>(0);
                BurstMethods.IsTextureEmptyRGB((Color24RGB*)texture.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr(),
                    mask ? (Color24RGB*)mask.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null,
                    texture.GetPixelData<Color24RGB>(0).Length, 
                    out isEmpty );
            }
            else
            if (texture.format == TextureFormat.RGBAFloat)
            {
                var pd = texture.GetPixelData<float4>(0);
                BurstMethods.IsTextureEmptyRGBAFloat((float4*)texture.GetPixelData<float4>(0).GetUnsafeReadOnlyPtr(),
                    mask ? (Color24RGB*)mask.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null,
                    texture.GetPixelData<float4>(0).Length,
                    ignoreAlpha,
                    out isEmpty );
            }
            else
            {
                Debug.LogError("Unsupported texture format for emptiness check: " + texture.format);
            }
            Debug.Log($"IsTextureEmpty took {isEmpty} for {texture.name} with format {texture.format}");
            return isEmpty;
        }
        
        public static RenderTexture CreateRenderTextureForMode(MaterialMode mode, TextureResolution resolution)
        {
            bool useHdr = mode == MaterialMode.Emission;
            var isLinear = BakeHelpers.IsDebugMaterialModeInLinear(mode);
            RenderTexture rt;
            if (mode == MaterialMode.NormalTangentSpace)
            {
                rt = RenderTexture.GetTemporary(resolution.width, resolution.height, 0, RenderTextureFormat.ARGBFloat, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                
            }
            else
                rt = RenderTexture.GetTemporary(resolution.width, resolution.height, 0, useHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);

            rt.filterMode = FilterMode.Bilinear;
            rt.antiAliasing = 2;
            return rt;
        }

        public static Texture2D CreateBakingTextureForMode(MaterialMode mode, TextureResolution resolution)
        {
            bool useHdr = mode == MaterialMode.Emission;
            var isLinear = BakeHelpers.IsDebugMaterialModeInLinear(mode);

            Texture2D bakeTex = null;
            if (mode == MaterialMode.NormalTangentSpace)
            {
                bakeTex = new Texture2D(resolution.width, resolution.height, TextureFormat.RGBAFloat, false, true);
            }
            else
                bakeTex = new Texture2D(resolution.width, resolution.height, useHdr ? TextureFormat.RGBAFloat : TextureFormat.RGB24, false, isLinear);
            bakeTex.name = $"Baked {mode}";
            bakeTex.wrapMode = TextureWrapMode.Repeat;
            bakeTex.filterMode = FilterMode.Bilinear;
            bakeTex.anisoLevel = 1;
            bakeTex.Apply();
            return bakeTex;
        }
        
        public static void DilateMap(Texture source, RenderTexture target, Color backgroundColor, Texture mask)
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
            _dilateMaterial.SetTexture("_MaskTex", mask);
            Graphics.Blit(source, target, _dilateMaterial);
        }
    }
}