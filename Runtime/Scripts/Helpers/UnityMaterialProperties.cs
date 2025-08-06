namespace UnityGLTF
{
    public static class UnityMaterialProperties
    {
        public static readonly string[] PBRMetallicRoughness = new string[]
        {
            "_Metallic",
            "_MetallicFactor",
            "metallicFactor",
            "_MetallicGlossMap",
            "_Glossiness",
            "_Roughness",
            "_RoughnessFactor",
            "roughnessFactor",
            "_MetallicRoughnessTexture",
            "metallicRoughnessTexture",
            "_Smoothness"
        };
        
        public static readonly string[] AlphaCutOff = new string[]
        {
            "alphaCutoff",
            "_AlphaCutoff",
            "_Cutoff",
            "AlphaCutOff",
        };

        public static readonly string[] NormalScale = new string[]
        {
            "normalScale",
            "_NormalScale",
            "_BumpScale"
        };
        
        public static readonly string[] NormalMap = new string[]
        {
            "normalTexture",
            "_NormalTexture",
            "_NormalMap",
            "_BumpMap",
            "_NormalTex",
            "_NormalMapTexture",
            "_NormalMapTex",
        };
        
        public static readonly string[] EmissionKeywords = new string[]
        {
            "_EMISSION",
            "_Emission",
            "EMISSION",
            "Emission",
        };

        public static readonly string[] EmissionColor = new string[]
        {
            "_EmissionColor",
            "emissiveFactor",
            "_EmissiveFactor",
            "_EmissiveColor",
        };
        
        public static readonly string[] EmissiveTexture = new string[]
        {
            "emissiveTexture",
            "_EmissiveTexture",
            "_EmissiveColorMap",
            "_EmissionMap",
            "_EmissiveMap",
        };
        
        public static readonly string[] OcclusionStrength = new string[]
        {
            "occlusionStrength",
            "_OcclusionStrength",
        };
        
        public static readonly string[] OcclusionTexture = new string[]
        {
            "_OcclusionMap", 
            "occlusionTexture", 
            "_OcclusionTexture", 
            "_MaskMap"
        };
        
        public static readonly string[] MetallicFactor = new string[]
        {
            "metallicFactor",
            "_MetallicFactor",
            "_Metallic",
        };
        
        public static readonly string[] MetallicRoughnessTexture = new string[]
        {
            "_MetallicGlossMap",
            "_MetallicRoughnessTexture",
            "metallicRoughnessTexture",
            "_MetallicRoughnessMap",
            "_MetallicRoughnessTex",
        };
        
        public static readonly string[] SmoothnessFactor = new string[]
        {
            "_Smoothness",
            "smoothnessFactor",
            "_SmoothnessFactor",
        };
        
        public static readonly string[] RoughnessFactor = new string[]
        {
            "roughnessFactor",
            "_RoughnessFactor",
            "_Roughness",
        };

        public static readonly string[] BaseColor = new string[]
        {
            "baseColorFactor",
            "_BaseColorFactor",
            "_BaseColor",
            "_Color",
            "_TinColor",
        };
        
        public static readonly string[] BaseColorTexture = new string[]
        {
            "_ColorTexture",
            "_MainTex",
            "_BaseMap",
            "_BaseColorTexture",
            "baseColorTexture",
            "_BaseColorMap",
            "_BaseColorTex",
        };

    }
}