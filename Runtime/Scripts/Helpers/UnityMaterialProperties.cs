namespace UnityGLTF
{
    public static class UnityMaterialProperties
    {
        public static readonly string[] IridescenceFactorIOR = new string[]
        {
            "iridescenceIor",
            "_IridescenceIor",
        };

        public static readonly string[] IOR = new string[]
        {
            "ior",
            "_IOR",
            "_IndexOfRefraction",
            "_IndexOfRefractionValue",
            "indexOfRefraction",
        };
        
        public static readonly string[] PBRMetallicRoughness = new string[]
        {
            "metallicFactor",
            "roughnessFactor",
            "metallicRoughnessTexture",
            "_MetallicFactor",
            "_RoughnessFactor",
            "_MetallicRoughnessTexture",
            "_MetallicGlossMap",
            "_Glossiness",
            "_Metallic",
            "_Roughness",
            "_Smoothness",
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
            "_BumpScale",
            "normalTextureScale",
        };
        
        public static readonly string[] NormalTexture = new string[]
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
            "emissiveFactor",
            "_EmissiveFactor",
            "_EmissionColor",
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
            "occlusionTextureStrength",
        };
        
        public static readonly string[] OcclusionTexture = new string[]
        {
            "occlusionTexture", 
            "_OcclusionTexture",
            "_OcclusionMap",  
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
            "metallicRoughnessTexture",
            "_MetallicRoughnessTexture",
            "_MetallicGlossMap",
            "_MetallicRoughnessMap",
            "_MetallicRoughnessTex",
        };
        
        public static readonly string[] SmoothnessFactor = new string[]
        {
            "smoothnessFactor",
            "_SmoothnessFactor",
            "_Smoothness",
            "_Glossiness",
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
            "baseColorTexture",
            "_BaseColorTexture",
            "_ColorTexture",
            "_BaseColorMap",
            "_BaseColorTex",
            "_BaseMap",
            "_MainTex",
        };

        public static readonly string[] TransmissionFactor = new string[]
        {
            "transmissionFactor",
            "_TransmissionFactor",
            "_Transmission",
        };

        public static readonly string[] ThicknessFactor = new string[]
        {
            "thicknessFactor",
            "_ThicknessFactor",
            "_Thickness",
        };

        public static readonly string[] AttenuationDistance = new string[]
        {
            "attenuationDistance",
            "_AttenuationDistance",
            "_Attenuation",
        };


        public static readonly string[] AttenuationColor = new string[]
        {
            "attenuationColor",
            "_AttenuationColor",
            "_AttenuationTint",
        };


        public static readonly string[] IridescenceFactor = new string[]
        {
            "iridescenceFactor",
            "_IridescenceFactor",
            "_Iridescence",
        };

        public static readonly string[] IridescenceThicknessMinimum = new string[]
        {
            "iridescenceThicknessMinimum",
            "_IridescenceThicknessMinimum",
            "_IridescenceMinThickness",
        };


        public static readonly string[] IridescenceThicknessMaximum = new string[]
        {
            "iridescenceThicknessMaximum",
            "_IridescenceThicknessMaximum",
            "_IridescenceMaxThickness",
        };

        public static readonly string[] SpecularFactor = new string[]
        {
            "specularFactor",
            "_SpecularFactor",
            "_Specular",
        };

        public static readonly string[] SpecularColor = new string[]
        {
            "_SpecularColorFactor",
            "specularColorFactor",
            "specularColor",
            "_SpecularColor",
        };

        public static readonly string[] ClearcoatFactor = new string[]
        {
            "clearcoatFactor",
            "_ClearcoatFactor",
            "_Clearcoat",
        };


        public static readonly string[] ClearcoatRoughnessFactor = new string[]
        {
            "clearcoatRoughnessFactor",
            "_ClearcoatRoughnessFactor",
            "_ClearcoatRoughness",
        };

        public static readonly string[] SheenRoughnessFactor = new string[]
        {
            "sheenRoughness",
            "_sheenRoughness",
            "sheenRoughnessFactor",
            "_SheenRoughnessFactor",
            "_SheenRoughness",
        };

        public static readonly string[] SheenColor = new string[]
        {
            "sheenColor",
            "_sheenColor",
            "_SheenColor",
            "_SheenColorFactor",
            "_sheenColorFactor",
            "sheenColorFactor",
            "_SheenTint",
        };

        public static readonly string[] ClearcoatTexture = new string[]
        {
            "_ClearcoatTexture",
            "clearcoatTexture",
            "ClearcoatTexture",
            "_ClearcoatMap",
            "_ClearcoatTex",
        };

        public static readonly string[] ClearcoatRoughnessTexture = new string[]
        {
            "_ClearcoatRoughnessTexture",
            "clearcoatRoughnessTexture",
            "ClearcoatRoughnessTexture",
            "_ClearcoatRoughnessMap",
            "_ClearcoatRoughnessTex",
        };

        public static readonly string[] ClearcoatNormalTexture = new string[]
        {
            "_ClearcoatNormalTexture",
            "clearcoatNormalTexture",
            "ClearcoatNormalTexture",
            "_ClearcoatNormalMap",
            "_ClearcoatNormalTex",
        };

        public static readonly string[] ThicknessTexture = new string[]
        {
            "thicknessTexture",
            "_thicknessTexture",
            "_ThicknessTexture",
            "_ThicknessMap",
            "_ThicknessTex",
        };

        public static readonly string[] TransmissionTexture = new string[]
        {
            "transmissionTexture",
            "_transmissionTexture",
            "_TransmissionTexture",
            "_TransmissionMap",
            "_TransmissionTex",
        };

        public static readonly string[] IridescenceTexture = new string[]
        {
            "iridescenceTexture",
            "_iridescenceTexture",
            "_IridescenceTexture",
            "_IridescenceMap",
            "_IridescenceTex",
        };

        public static readonly string[] IridescenceThicknessTexture = new string[]
        {
            "iridescenceThicknessTexture",
            "_iridescenceThicknessTexture",
            "_IridescenceThicknessTexture",
            "_IridescenceThicknessMap",
            "_IridescenceThicknessTex",
        };
        
        public static readonly string[] SpecularTexture = new string[]
        {
            "specularTexture", 
            "_specularTexture",
        };

        public static readonly string[] SpecularColorTexture = new string[]
        {
            "specularColorTexture",
            "_SpecularColorTexture",
            "_SpecularColorMap",
            "_SpecularColorTex",
        };

        public static readonly string[] SheenColorTexture = new string[]
        {
            "sheenColorTexture",
            "_sheenColorTexture",
            "_SheenColorTexture",
            "_SheenColorMap",
            "_SheenColorTex",
        };

        public static readonly string[] SheenRoughnessTexture = new string[]
        {
            "sheenRoughnessTexture",
            "_sheenRoughnessTexture",
            "_SheenRoughnessTexture",
            "_SheenRoughnessMap",
            "_SheenRoughnessTex",
        };

        public static readonly string[] AnisotropyTexture = new string[]
        {
            "anisotropyTexture",
            "_anisotropyTexture",
            "_AnisotropyTexture",
            "_AnisotropyMap",
            "_anisotropyMap",
            "anisotropyMap",
            "_AnisotropyTex",
        };

        public static readonly string[] AnisotropyStrength = new string[]
        {
            "anisotropyStrength",
            "_anisotropyStrength",
            "anisotropyFactor",
            "_anisotropyFactor",
            "_AnisotropyStrength",
            "_Anisotropy",
            "anisotropyStrength",
        };

        public static readonly string[] AnisotropyRotation = new string[]
        {
            "anisotropyRotation",
            "_anisotropyRotation",
            "anisotropyDirection",
            "_anisotropyDirection",
            "anisotropyAngle",
            "_anisotropyAngle",
            "_AnisotropyRotation",
            "_AnisotropyAngle",
            "anisotropyRotation",
        };

        public static readonly string[] Dispersion = new string[]
        {
            "dispersion",
            "_dispersion",
            "Dispersion",
            "_Dispersion",
            "_DispersionFactor",
            "dispersionFactor",
        };
    }
}