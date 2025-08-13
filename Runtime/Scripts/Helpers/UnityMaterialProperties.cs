using UnityEngine;

namespace UnityGLTF
{
    public static class UnityMaterialProperties
    {
#region Classes
        public class PropertyCollection
        {
            public readonly string[] names;
            public readonly int[] ids;
            
            
            public PropertyCollection(string[] names)
            {
                this.names = names;
                ids = new int[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    ids[i] = Shader.PropertyToID(names[i]);
                }
            }
            
            public string this[int param]
            {
                get => names[param];
            }
            
            
            public bool HasProperty(Material material)
            {
                if (material == null) return false;

                foreach (var id in ids)
                {
                    if (material.HasProperty(id))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        public class FloatProperties : PropertyCollection
        {

            public FloatProperties(string[] names) : base(names)
            {
            }

            public bool TryGetFloat(Material material, out float value, out string propertyName)
            {
                value = 0;
                propertyName = null;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (material.HasProperty(ids[i]))
                    {
                        propertyName = names[i];
                        value = material.GetFloat(ids[i]);
                        return true;
                    }
                }
                return false;
            }
        }
        
        public class ColorProperties : PropertyCollection
        {
            public ColorProperties(string[] names) : base(names)
            {
            }

            public bool TryGetColor(Material material, out Color color, out string propertyName)
            {
                color = Color.white;
                propertyName = null;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (material.HasProperty(ids[i]))
                    {
                        propertyName = names[i];
                        color = material.GetColor(ids[i]);
                        return true;
                    }
                }
                return false;
            }
        }
        
        public class IntProperties : PropertyCollection
        {
            public IntProperties(string[] names) : base(names)
            {
            }

            public bool TryGetInt(Material material, out int value, out string propertyName)
            {
                value = 0;
                propertyName = null;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (material.HasInteger(ids[i]))
                    {
                        propertyName = names[i];
                        value = material.GetInteger(ids[i]);
                        return true;
                    }
                    else if (material.HasFloat(ids[i]))
                    {
                        propertyName = names[i];
                        value = Mathf.RoundToInt(material.GetFloat(ids[i]));
                        return true;
                    }
                }
                return false;
            }
        }
        
        
        public class TextureProperties : PropertyCollection
        {
            public readonly int[] ids_ST;
            public readonly int[] ids_TexCoord;
            public readonly int[] ids_Rotation;

            public TextureProperties(string[] names) : base(names)
            {
                ids_ST = new int[names.Length];
                ids_TexCoord = new int[names.Length];
                ids_Rotation = new int[names.Length];
                
                for (int i = 0; i < names.Length; i++)
                {
                    ids_ST[i] = Shader.PropertyToID(names[i] + "_ST");
                    ids_TexCoord[i] = Shader.PropertyToID(names[i] + "TexCoord");
                    ids_Rotation[i] = Shader.PropertyToID(names[i] + "Rotation");
                }
            }
            
            public bool TryGetTexture(Material material, out Texture texture, out string texturePropName)
            {
                texture = null;
                texturePropName = null;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (material.HasProperty(ids[i]))
                    {
                        texturePropName = names[i];
                        texture = material.GetTexture(ids[i]);
                        return true;
                    }
                }
         
                return false;
            }
            
            public bool TryGetTextureUVChannel(Material material, out int uvChannel)
            {
                uvChannel = 0;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (material.HasProperty(ids_TexCoord[i]))
                    {
                        uvChannel = Mathf.RoundToInt(material.GetFloat(ids_TexCoord[i]));
                        return true;
                    }
                }
                return false;
            }
            
            public bool TryGetTextureTransform(Material material, out Vector2 scale, out Vector2 offset, out float rotation)
            {
                scale = Vector2.one;
                offset = Vector2.zero;
                rotation = 0;
			
                for (int i = 0; i < ids.Length; i++)
                {
                    if(names[i] == "_MainTex")
                    {
                        offset = material.mainTextureOffset;
                        scale = material.mainTextureScale;
                        rotation = 0;
#if UNITY_2021_1_OR_NEWER
                        if (material.HasFloat("_MainTexRotation"))
#else
						if (mat.HasProperty("_MainTexRotation"))
#endif
                            rotation = material.GetFloat("_MainTexRotation");

                        return true;
                    }

				
                    if (material.HasProperty(ids_ST[i]) && material.HasProperty(ids[i]))
                    {
                        scale = material.GetTextureScale(ids[i]);
                        offset = material.GetTextureOffset(ids[i]);
                        rotation = material.HasProperty(ids_Rotation[i]) ? material.GetFloat(ids_Rotation[i]) : 0;

                        return true;
                    }
                }
                return false;
			
            }
            
        }
        
        public static bool HasPropertyInMaterial(Material material, params string[] names)
        {
            if (material == null) return false;

            foreach (var name in names)
            {
                if (material.HasProperty(name))
                {
                    return true;
                }
            }
            return false;
        }
#endregion
        
        public static readonly FloatProperties IridescenceFactorIOR = new( new string[]
        {
            "iridescenceIor",
            "_IridescenceIor",
        });

        public static readonly FloatProperties IOR = new( new string[]
        {
            "ior",
            "_IOR",
            "_IndexOfRefraction",
            "_IndexOfRefractionValue",
            "indexOfRefraction",
        });
        
        public static readonly PropertyCollection PBRMetallicRoughness = new( new string[]
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
        });
        
        public static readonly FloatProperties AlphaCutOff = new( new string[]
        {
            "alphaCutoff",
            "_AlphaCutoff",
            "_Cutoff",
            "AlphaCutOff",
        });

        public static readonly FloatProperties NormalScale = new( new string[]
        {
            "normalScale",
            "_NormalScale",
            "_BumpScale",
            "normalTextureScale",
        });
        
        public static readonly TextureProperties NormalTexture = new( new string[]
        {
            "normalTexture",
            "_NormalTexture",
            "_NormalMap",
            "_BumpMap",
            "_NormalTex",
            "_NormalMapTexture",
            "_NormalMapTex",
        });
        
        public static readonly string[] EmissionKeywords = new string[]
        {
            "_EMISSION",
            "_Emission",
            "EMISSION",
            "Emission",
        };

        public static readonly ColorProperties EmissionColor = new( new string[]
        {
            "emissiveFactor",
            "_EmissiveFactor",
            "_EmissionColor",
            "_EmissiveColor",
        });
        
        public static readonly TextureProperties EmissiveTexture = new( new string[]
        {
            "emissiveTexture",
            "_EmissiveTexture",
            "_EmissiveColorMap",
            "_EmissionMap",
            "_EmissiveMap",
        });
        
        public static readonly FloatProperties OcclusionStrength = new( new string[]
        {
            "occlusionStrength",
            "_OcclusionStrength",
            "occlusionTextureStrength",
        });
        
        public static readonly TextureProperties OcclusionTexture = new( new string[]
        {
            "occlusionTexture", 
            "_OcclusionTexture",
            "_OcclusionMap",  
            "_MaskMap"
        });
        
        public static readonly FloatProperties MetallicFactor = new( new string[]
        {
            "metallicFactor",
            "_MetallicFactor",
            "_Metallic",
        });
        
        public static readonly TextureProperties MetallicRoughnessTexture = new( new string[]
        {
            "metallicRoughnessTexture",
            "_MetallicRoughnessTexture",
            "_MetallicGlossMap",
            "_MetallicRoughnessMap",
            "_MetallicRoughnessTex",
        });
        
        public static readonly FloatProperties SmoothnessFactor = new( new string[]
        {
            "smoothnessFactor",
            "_SmoothnessFactor",
            "_Smoothness",
            "_Glossiness",
        });
        
        public static readonly FloatProperties RoughnessFactor = new( new string[]
        {
            "roughnessFactor",
            "_RoughnessFactor",
            "_Roughness",
        });

        public static readonly ColorProperties BaseColor = new( new string[]
        {
            "baseColorFactor",
            "_BaseColorFactor",
            "_BaseColor",
            "_Color",
            "_TinColor",
        });
        
        public static readonly TextureProperties BaseColorTexture = new( new string[]
        {
            "baseColorTexture",
            "_BaseColorTexture",
            "_ColorTexture",
            "_BaseColorMap",
            "_BaseColorTex",
            "_BaseMap",
            "_MainTex",
        });

        public static readonly FloatProperties TransmissionFactor = new( new string[]
        {
            "transmissionFactor",
            "_TransmissionFactor",
            "_Transmission",
        });

        public static readonly FloatProperties ThicknessFactor = new( new string[]
        {
            "thicknessFactor",
            "_ThicknessFactor",
            "_Thickness",
        });

        public static readonly FloatProperties AttenuationDistance = new( new string[]
        {
            "attenuationDistance",
            "_AttenuationDistance",
            "_Attenuation",
        });
        
        public static readonly ColorProperties AttenuationColor = new( new string[]
        {
            "attenuationColor",
            "_AttenuationColor",
            "_AttenuationTint",
        });
        
        public static readonly FloatProperties IridescenceFactor = new( new string[]
        {
            "iridescenceFactor",
            "_IridescenceFactor",
            "_Iridescence",
        });

        public static readonly FloatProperties IridescenceThicknessMinimum = new( new string[]
        {
            "iridescenceThicknessMinimum",
            "_IridescenceThicknessMinimum",
            "_IridescenceMinThickness",
        });
        
        public static readonly FloatProperties IridescenceThicknessMaximum = new( new string[]
        {
            "iridescenceThicknessMaximum",
            "_IridescenceThicknessMaximum",
            "_IridescenceMaxThickness",
        });

        public static readonly FloatProperties SpecularFactor = new( new string[]
        {
            "specularFactor",
            "_SpecularFactor",
            "_Specular",
        });

        public static readonly ColorProperties SpecularColor = new( new string[]
        {
            "_SpecularColorFactor",
            "specularColorFactor",
            "specularColor",
            "_SpecularColor",
        });

        public static readonly FloatProperties ClearcoatFactor = new( new string[]
        {
            "clearcoatFactor",
            "_ClearcoatFactor",
            "_Clearcoat",
        });
        
        public static readonly FloatProperties ClearcoatRoughnessFactor = new( new string[]
        {
            "clearcoatRoughnessFactor",
            "_ClearcoatRoughnessFactor",
            "_ClearcoatRoughness",
        });

        public static readonly FloatProperties SheenRoughnessFactor = new( new string[]
        {
            "sheenRoughness",
            "_sheenRoughness",
            "sheenRoughnessFactor",
            "_SheenRoughnessFactor",
            "_SheenRoughness",
        });

        public static readonly ColorProperties SheenColor = new( new string[]
        {
            "sheenColor",
            "_sheenColor",
            "_SheenColor",
            "_SheenColorFactor",
            "_sheenColorFactor",
            "sheenColorFactor",
            "_SheenTint",
        });

        public static readonly TextureProperties ClearcoatTexture = new( new string[]
        {
            "_ClearcoatTexture",
            "clearcoatTexture",
            "ClearcoatTexture",
            "_ClearcoatMap",
            "_ClearcoatTex",
        });

        public static readonly TextureProperties ClearcoatRoughnessTexture = new( new string[]
        {
            "_ClearcoatRoughnessTexture",
            "clearcoatRoughnessTexture",
            "ClearcoatRoughnessTexture",
            "_ClearcoatRoughnessMap",
            "_ClearcoatRoughnessTex",
        });

        public static readonly TextureProperties ClearcoatNormalTexture = new( new string[]
        {
            "_ClearcoatNormalTexture",
            "clearcoatNormalTexture",
            "ClearcoatNormalTexture",
            "_ClearcoatNormalMap",
            "_ClearcoatNormalTex",
        });

        public static readonly TextureProperties ThicknessTexture = new( new string[]
        {
            "thicknessTexture",
            "_thicknessTexture",
            "_ThicknessTexture",
            "_ThicknessMap",
            "_ThicknessTex",
        });

        public static readonly TextureProperties TransmissionTexture = new( new string[]
        {
            "transmissionTexture",
            "_transmissionTexture",
            "_TransmissionTexture",
            "_TransmissionMap",
            "_TransmissionTex",
        });

        public static readonly TextureProperties IridescenceTexture = new( new string[]
        {
            "iridescenceTexture",
            "_iridescenceTexture",
            "_IridescenceTexture",
            "_IridescenceMap",
            "_IridescenceTex",
        });

        public static readonly TextureProperties IridescenceThicknessTexture = new( new string[]
        {
            "iridescenceThicknessTexture",
            "_iridescenceThicknessTexture",
            "_IridescenceThicknessTexture",
            "_IridescenceThicknessMap",
            "_IridescenceThicknessTex",
        });
        
        public static readonly TextureProperties SpecularTexture = new( new string[]
        {
            "specularTexture", 
            "_specularTexture",
        });

        public static readonly TextureProperties SpecularColorTexture = new( new string[]
        {
            "specularColorTexture",
            "_SpecularColorTexture",
            "_SpecularColorMap",
            "_SpecularColorTex",
        });

        public static readonly TextureProperties SheenColorTexture = new( new string[]
        {
            "sheenColorTexture",
            "_sheenColorTexture",
            "_SheenColorTexture",
            "_SheenColorMap",
            "_SheenColorTex",
        });

        public static readonly TextureProperties SheenRoughnessTexture = new( new string[]
        {
            "sheenRoughnessTexture",
            "_sheenRoughnessTexture",
            "_SheenRoughnessTexture",
            "_SheenRoughnessMap",
            "_SheenRoughnessTex",
        });

        public static readonly TextureProperties AnisotropyTexture = new( new string[]
        {
            "anisotropyTexture",
            "_anisotropyTexture",
            "_AnisotropyTexture",
            "_AnisotropyMap",
            "_anisotropyMap",
            "anisotropyMap",
            "_AnisotropyTex",
        });

        public static readonly FloatProperties AnisotropyStrength = new( new string[]
        {
            "anisotropyStrength",
            "_anisotropyStrength",
            "anisotropyFactor",
            "_anisotropyFactor",
            "_AnisotropyStrength",
            "_Anisotropy",
            "anisotropyStrength",
        });

        public static readonly FloatProperties AnisotropyRotation = new( new string[]
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
        });

        public static readonly FloatProperties Dispersion = new( new string[]
        {
            "dispersion",
            "_dispersion",
            "Dispersion",
            "_Dispersion",
            "_DispersionFactor",
            "dispersionFactor",
        });

        public static readonly IntProperties CullMode = new( new string[]
        {
            "_Cull",
            "_CullMode",
            "cullMode",
            "cull",
            "Culling",
            "_Culling",
        });
    }
}