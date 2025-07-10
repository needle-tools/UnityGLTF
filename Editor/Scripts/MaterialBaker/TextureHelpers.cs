using UnityEngine;

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
                
                if (lastColor.HasValue && !ColorProximity(pixelData[i], lastColor.Value))
                {
                    singleValue = Color.clear;
                    return false; // found different color
                }
   
                lastColor = pixelData[i];
                
            }
            if (lastColor == null)
            {
                singleValue = Color.clear;
                return false; // no pixels found
            }
            
            singleValue = lastColor.Value;

            return true;
        }
        
        public static bool IsTextureEmpty(Texture2D texture, bool ignoreAlpha = true, Texture2D mask = null)
        {
            var pixelData = texture.GetPixelData<Color32>(0);
            var maskData = mask?.GetPixels();

            float proximityThreshold = 0.001f; // threshold for color proximity check
            bool hasData = false;
            for (int i = 0; i < pixelData.Length; i++)
            {
                if (maskData != null && maskData[i] == Color.black)
                    continue; // skip masked pixels

                hasData |= (!ignoreAlpha && pixelData[i].a > proximityThreshold) || pixelData[i].r > proximityThreshold || pixelData[i].g > proximityThreshold || pixelData[i].b > proximityThreshold;
                if (hasData)
                    break;
            }

            return !hasData;
        }
        
        public static RenderTexture CreateRenderTextureForMode(MaterialMode mode, TextureResolution resolution)
        {
            bool useHdr = mode == MaterialMode.Emission;
            var isLinear = BakeHelpers.IsDebugMaterialModeInLinear(mode);
            var rt = RenderTexture.GetTemporary(resolution.width, resolution.height, 0, useHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;
            rt.antiAliasing = 3;
            return rt;
        }

        public static Texture2D CreateBakingTextureForMode(MaterialMode mode, TextureResolution resolution)
        {
            bool useHdr = mode == MaterialMode.Emission;
            var isLinear = BakeHelpers.IsDebugMaterialModeInLinear(mode);
            
            var bakeTex = new Texture2D(resolution.width, resolution.height, useHdr ? TextureFormat.RGBAFloat : TextureFormat.RGB24, false, isLinear);
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