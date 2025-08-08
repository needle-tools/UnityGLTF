using System;
using System.Collections.Generic;
using System.Linq;
using GLTF.Schema;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF.Extensions;
using UnityGLTF.Plugins;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityGLTF
{
	public partial class GLTFSceneExporter
	{
		// Static callbacks
		[Obsolete("Use ExportPlugins on GLTFSettings instead")]
		public static event BeforeSceneExportDelegate BeforeSceneExport;
		[Obsolete("Use ExportPlugins on GLTFSettings instead")]
		public static event AfterSceneExportDelegate AfterSceneExport;
		[Obsolete("Use ExportPlugins on GLTFSettings instead")]
		public static event AfterNodeExportDelegate AfterNodeExport;
		/// <returns>True: material export is complete. False: continue regular export.</returns>
		[Obsolete("Use ExportPlugins on GLTFSettings instead")]
		public static event BeforeMaterialExportDelegate BeforeMaterialExport;
		[Obsolete("Use ExportPlugins on GLTFSettings instead")]
		public static event AfterMaterialExportDelegate AfterMaterialExport;
		
		// mock for Default material
		private static Material _defaultMaterial = null;
		public static Material DefaultMaterial
		{
			get
			{
				if (_defaultMaterial) return _defaultMaterial;

#if UNITY_2019_3_OR_NEWER
				var pipelineAsset = GraphicsSettings.currentRenderPipeline;
				if (pipelineAsset)
				{
					_defaultMaterial = pipelineAsset.defaultMaterial;
				}
				else
#endif
				{
					var shader = Shader.Find("Legacy Shaders/Diffuse"); // by default in the always included shaders list
					if (shader)
						_defaultMaterial = new Material(shader);
				}
				return _defaultMaterial;
			}
		}

		// These occlusion maps are baked into metallic roughness map (channel R)
		private HashSet<Material> _occlusionBakedTextures = new HashSet<Material>();


		private bool TryGetColorFromMaterial(Material material, out Color color, params string[] propertyNames)
		{
			color = Color.white;
			foreach (var name in propertyNames)
			{
				if (material.HasProperty(name))
				{
					color = material.GetColor(name);
					return true;
				}
			}
			return false;
		}

		private bool TryGetFloatFromMaterial(Material material, out float value, params string[] propertyNames)
		{
			value = 0f;
			foreach (var name in propertyNames)
			{
				if (material.HasProperty(name))
				{
					value = material.GetFloat(name);
					return true;
				}
			}
			return false;
		}
		
		private bool TryGetIntFromMaterial(Material material, out int value, params string[] propertyNames)
		{
			value = 0;
			foreach (var name in propertyNames)
			{
				if (material.HasProperty(name))
				{
					value = material.GetInteger(name);
					return true;
				}
			}
			return false;
		}
		
		private bool HasPropertyInMaterial(Material material, params string[] propertyNames)
		{
			if (material == null) return false;

			foreach (var name in propertyNames)
			{
				if (material.HasProperty(name))
				{
					return true;
				}
			}
			return false;
		}

		private bool TryGetTextureTransform(Material material, out Vector2 scale, out Vector2 offset, out float rotation,
			params string[] textureNames)
		{
			scale = Vector2.one;
			offset = Vector2.zero;
			rotation = 0;
			
			foreach (var name in textureNames)
			{
				
				if(name == "_MainTex")
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

				
				if (material.HasProperty(name+ "_ST") && material.HasProperty(name))
				{
					scale = material.GetTextureScale(name);
					offset = material.GetTextureOffset(name);
					rotation = material.HasProperty(name+ "Rotation") ? material.GetFloat(name+"Rotation") : 0;

					return true;
				}
			}
			return false;
			
		}
		
		private bool TryGetTextureFromMaterial(Material material, out Texture texture, out string texturePropName, params string[] propertyNames)
		{
			texture = null;
			texturePropName = null;
			foreach (var name in propertyNames)
			{
				if (material.HasProperty(name))
				{
					texturePropName = name;
					texture = material.GetTexture(name);
					return true;
				}
			}
			return false;
		}

		private bool TryGetTextureUVChannel(Material material, out int uvChannel, params string[] propertyNames)
		{
			uvChannel = 0;
			foreach (var name in propertyNames)
			{
				var nameWithTexCoord = name + "TexCoord";
				if (material.HasProperty(nameWithTexCoord))
				{
					uvChannel = Mathf.RoundToInt(material.GetFloat(nameWithTexCoord));
					return true;
				}
			}
			return false;
		}

		private bool HasKeywordEnabled(Material material, params string[] keywords)
		{
			if (material == null) return false;

			foreach (var keyword in keywords)
			{
				if (material.IsKeywordEnabled(keyword))
				{
					return true;
				}
			}
			return false;
		}
		
        public MaterialId ExportMaterial(Material materialObj)
		{
			MaterialId id = GetMaterialId(_root, materialObj);
			if (id != null)
			{
				return id;
			}

			var material = new GLTFMaterial();

            if (!materialObj)
            {
                if (ExportNames)
                {
                    material.Name = "default";
                }

                // create default material
                // TODO check why we need to do anything here
                material.PbrMetallicRoughness = new PbrMetallicRoughness() { MetallicFactor = 0, RoughnessFactor = 1.0f };
                return CreateAndAddMaterialId(materialObj, material);
            }

			if (ExportNames)
			{
				material.Name = materialObj.name;
			}

			foreach (var plugin in _plugins)
			{
				if (plugin == null) continue;
				beforeMaterialExportMarker.Begin();
				if (plugin.BeforeMaterialExport(this, _root, materialObj, material))
				{
					beforeMaterialExportMarker.End();
					return CreateAndAddMaterialId(materialObj, material);
				}
				else
				{
					beforeMaterialExportMarker.End();
				}
			}

			exportMaterialMarker.Begin();
			var isBirp =
#if UNITY_2019_3_OR_NEWER
				!GraphicsSettings.currentRenderPipeline;
#else
				true;
#endif

			switch (materialObj.GetTag("RenderType", false, ""))
			{
				case "TransparentCutout":
					if (TryGetFloatFromMaterial(materialObj, out var alphaCutOff, UnityMaterialProperties.AlphaCutOff))
						material.AlphaCutoff = alphaCutOff;
					
					material.AlphaMode = AlphaMode.MASK;
					break;
				case "Transparent":
				case "Fade":
					material.AlphaMode = AlphaMode.BLEND;
					break;
				default:
					if ((!isBirp && materialObj.IsKeywordEnabled("_ALPHATEST_ON")) ||
					    (isBirp && materialObj.IsKeywordEnabled("_BUILTIN_ALPHATEST_ON")) ||
					    materialObj.renderQueue == 2450)
					{
						if (TryGetFloatFromMaterial(materialObj, out var alphaCutOff2, UnityMaterialProperties.AlphaCutOff))
							material.AlphaCutoff = alphaCutOff2;
						material.AlphaMode = AlphaMode.MASK;
					}
					else
					{
						material.AlphaMode = AlphaMode.OPAQUE;
					}
					break;
			}

			material.DoubleSided = (TryGetIntFromMaterial(materialObj, out var cullMode, UnityMaterialProperties.CullMode) && cullMode == (int)CullMode.Off)
									|| materialObj.shader.name.EndsWith("-Double");  // workaround for exporting shaders that are set to double-sided on 2020.3

			if (HasKeywordEnabled(materialObj, UnityMaterialProperties.EmissionKeywords)
			    || TryGetTextureFromMaterial(materialObj, out _, out _, UnityMaterialProperties.EmissiveTexture))
			{
				// In Gamma space, some materials treat their emissive color inputs differently than in Linear space.
				// This is super confusing when converting materials, but we also need to handle it correctly here.
				var isUnityMaterialWithWeirdColorspaceHandling = QualitySettings.activeColorSpace == ColorSpace.Gamma &&
					(materialObj.shader.name == "Standard" || materialObj.shader.name == "Standard (Specular setup)" ||
					materialObj.shader.name == "Standard (Roughness setup)" ||
					materialObj.shader.name == "Universal Render Pipeline/Lit" ||
					materialObj.shader.name == "Universal Render Pipeline/Simple Lit" ||
					materialObj.shader.name == "Universal Render Pipeline/Unlit");
				        
				if (TryGetColorFromMaterial(materialObj, out var emissionColor, UnityMaterialProperties.EmissionColor)
				    || materialObj.HasProperty("_UseEmissiveIntensity"))
				{
					var emissiveAmount = Color.black;
					var maxEmissiveAmount = 0f;
					if (materialObj.HasProperty("_UseEmissiveIntensity"))
					{
						// hdrp route uses its own color decomposition
						if (materialObj.GetFloat("_UseEmissiveIntensity") == 1)
						{
							emissiveAmount = materialObj.GetColor("_EmissiveColorLDR");
							maxEmissiveAmount = materialObj.GetFloat("_EmissiveIntensity");
						}
						else
						{
							var colorHdr = materialObj.GetColor("_EmissiveColor");
							ConvertHDRColorToLDR(colorHdr, out emissiveAmount, out maxEmissiveAmount);
						}
					}
					else
					{
						var c = emissionColor;
						DecomposeEmissionColor(c, out emissiveAmount, out maxEmissiveAmount);
					}
					
					if (isUnityMaterialWithWeirdColorspaceHandling)
						material.EmissiveFactor = emissiveAmount.ToNumericsColorRaw();
					else
						material.EmissiveFactor = emissiveAmount.ToNumericsColorGamma();

					if (maxEmissiveAmount > 1)
					{
						var materialSettings = (_plugins.FirstOrDefault(x => x is MaterialExtensionsExportContext) as MaterialExtensionsExportContext)?.settings;
						var emissiveStrengthSupported = materialSettings && materialSettings.KHR_materials_emissive_strength;
						if (emissiveStrengthSupported)
						{
							material.AddExtension(KHR_materials_emissive_strength_Factory.EXTENSION_NAME, new KHR_materials_emissive_strength() { emissiveStrength = maxEmissiveAmount });
							DeclareExtensionUsage(KHR_materials_emissive_strength_Factory.EXTENSION_NAME, false);
						}
					}
				}

				if (TryGetTextureFromMaterial(materialObj, out var emissionTex, out var propName, UnityMaterialProperties.EmissiveTexture))
				{
					if (emissionTex)
					{
						if(emissionTex is Texture2D)
						{
							material.EmissiveTexture = ExportTextureInfo(emissionTex, TextureMapType.Emissive);

							ExportTextureTransform(material.EmissiveTexture, materialObj, propName);
						}
						else
						{
							Debug.LogFormat(LogType.Error, "Can't export a {0} emissive texture in material {1}", emissionTex.GetType(), materialObj.name);
						}

					}
				}
			}

			// workaround for glTFast roundtrip: has a _BumpMap but no _NORMALMAP or _BUMPMAP keyword.
			var is_glTFastShader = materialObj.shader.name.IndexOf("glTF", StringComparison.Ordinal) > -1;
			if (materialObj.HasProperty("_BumpMap") && (materialObj.IsKeywordEnabled("_NORMALMAP") || materialObj.IsKeywordEnabled("_BUMPMAP") || is_glTFastShader))
			{
				var normalTex = materialObj.GetTexture("_BumpMap");

				if (normalTex)
				{
					if(normalTex is Texture2D)
					{
						material.NormalTexture = ExportNormalTextureInfo(normalTex, TextureMapType.Normal, materialObj);
						ExportTextureTransform(material.NormalTexture, materialObj, "_BumpMap");
					}
					else
					{
						Debug.LogFormat(LogType.Error, "Can't export a {0} normal texture in material {1}", normalTex.GetType(), materialObj.name);
					}
				}
			}
			else
			if (TryGetTextureFromMaterial(materialObj, out var normalTexture, out var normalTexProp, UnityMaterialProperties.NormalTexture))
			{
				if (normalTexture)
				{
					if(normalTexture is Texture2D)
					{
						material.NormalTexture = ExportNormalTextureInfo(normalTexture, TextureMapType.Normal, materialObj);
						ExportTextureTransform(material.NormalTexture, materialObj, normalTexProp);
					}
					else
					{
						Debug.LogFormat(LogType.Error, "Can't export a {0} normal texture in material {1}", normalTexture.GetType(), materialObj.name);
					}
				}
			}
			
			if (IsUnlit(materialObj))
			{
				ExportUnlit( material, materialObj );
			}
			else if (IsPBRMetallicRoughness(materialObj))
			{
				material.PbrMetallicRoughness = ExportPBRMetallicRoughness(materialObj);
			}
			else if (IsPBRSpecularGlossiness(materialObj))
			{
				ExportPBRSpecularGlossiness(material, materialObj);
			}
			else if (IsCommonConstant(materialObj))
			{
				material.CommonConstant = ExportCommonConstant(materialObj);
			}
			else
			{
				if (TryGetTextureFromMaterial(materialObj, out var baseColTex, out var baseColTexProp,
					    UnityMaterialProperties.BaseColorTexture))
				{
					material.PbrMetallicRoughness = new PbrMetallicRoughness()
					{
						BaseColorFactor = (TryGetColorFromMaterial(materialObj, out var bCol,
							UnityMaterialProperties.BaseColor)
							? bCol
							: Color.white).ToNumericsColorLinear(),
						BaseColorTexture = baseColTex ? ExportTextureInfo(baseColTex, TextureMapType.BaseColor) : null
					};
					ExportTextureTransform(material.PbrMetallicRoughness.BaseColorTexture, materialObj, baseColTexProp);
				}

				
				if (materialObj.HasProperty("_MainTex")) //else export main texture
					material.DoubleSided = true;
			}

			if (TryGetTextureFromMaterial(materialObj, out var occTex, out var occlTexProp, UnityMaterialProperties.OcclusionTexture))
			{
				
				if (occTex)
				{
					if(occTex is Texture2D)
					{
						TextureId sharedTextureId = null;
						if (material.PbrMetallicRoughness != null &&
						    material.PbrMetallicRoughness.MetallicRoughnessTexture != null)
						{
							if (_occlusionBakedTextures.Contains(materialObj))
							{
								sharedTextureId = material.PbrMetallicRoughness.MetallicRoughnessTexture.Index;
							}
						}
						
						material.OcclusionTexture = ExportOcclusionTextureInfo(occTex, TextureMapType.Occlusion, materialObj, sharedTextureId);
						ExportTextureTransform(material.OcclusionTexture, materialObj, occlTexProp);

						if (TryGetFloatFromMaterial(materialObj, out var ooclTexCoord, "occlusionTextureTexCoord", "_OcclusionTextureTexCoord"))
							material.OcclusionTexture.TexCoord = Mathf.RoundToInt(ooclTexCoord);
						else
							material.OcclusionTexture.TexCoord = 0;
					}
					else
					{
						Debug.LogFormat(LogType.Error, "Can't export a {0} occlusion texture in material {1}", occTex.GetType(), materialObj.name);
					}
				}
			}
			exportMaterialMarker.End();

			return CreateAndAddMaterialId(materialObj, material);
		}

        private MaterialId CreateAndAddMaterialId(Material materialObj, GLTFMaterial material)
        {
	        var key = materialObj ? materialObj.GetInstanceID() : 0;
	        if(!_exportedMaterials.ContainsKey(key))
				_exportedMaterials.Add(key, _root.Materials.Count);

	        var id = new MaterialId
	        {
		        Id = _root.Materials.Count,
		        Root = _root
	        };
	        _root.Materials.Add(material);

	        // after material export
	        if (materialObj)
	        {
		        afterMaterialExportMarker.Begin();
		        foreach (var plugin in _plugins)
			        plugin?.AfterMaterialExport(this, _root, materialObj, material);
		        afterMaterialExportMarker.End();
	        }

	        return id;
        }

        private bool IsPBRMetallicRoughness(Material material)
        {
	        return HasPropertyInMaterial(material, UnityMaterialProperties.PBRMetallicRoughness);
        }

        private bool IsUnlit(Material material)
        {
	        return material.shader.name.ToLowerInvariant().Contains("unlit") || material.shader.name == "Sprites/Default";
        }

        private bool IsPBRSpecularGlossiness(Material material)
        {
	        return HasPropertyInMaterial(material, "_SpecColor", "_SpecGlossMap");
        }

        private bool IsCommonConstant(Material material)
        {
	        return  HasPropertyInMaterial(material, "_AmbientFactor", "_LightMap", "_LightFactor");
        }

#if UNITY_2019_1_OR_NEWER
        private static bool CheckForPropertyInShader(Shader shader, string name, ShaderPropertyType type)
        {
	        // TODO result can be cached, we might do many similar checks for one export

	        var c = shader.GetPropertyCount();
	        var foundProperty = false;
	        for (var i = 0; i < c; i++)
	        {
		        if (shader.GetPropertyName(i) == name && shader.GetPropertyType(i) == type)
		        {
			        foundProperty = true;
			        break;
		        }
	        }
	        return foundProperty;
        }
#endif

		private void ExportTextureTransform(TextureInfo def, Material mat, string texName)
		{
			if (def == null) return;

			// early out if texture transform is explicitly disabled
			if (mat.HasProperty("_TEXTURE_TRANSFORM") && !mat.IsKeywordEnabled("_TEXTURE_TRANSFORM_ON"))
				return;

			Vector2 offset = mat.GetTextureOffset(texName);
			Vector2 scale = mat.GetTextureScale(texName);
			//var rotationMatrix = mat.GetVector(texName + "Rotation");
			//var rotation = -Mathf.Atan2(offset.y, rotationMatrix.x);
			var rotProp = texName + "Rotation";
			var uvProp = texName + "TexCoord";
			var rotation = 0f;
			var uvChannel = 0f;

#if UNITY_2021_1_OR_NEWER
			if (mat.HasFloat(rotProp))
#else
			if (mat.HasProperty(rotProp)
#if UNITY_2019_1_OR_NEWER
				&& CheckForPropertyInShader(mat.shader, rotProp, ShaderPropertyType.Float)
#endif
			)
#endif
				rotation = mat.GetFloat(rotProp);

#if UNITY_2021_1_OR_NEWER
			if (mat.HasFloat(uvProp))
#else
			if (mat.HasProperty(uvProp)
#if UNITY_2019_1_OR_NEWER
				&& CheckForPropertyInShader(mat.shader, uvProp, ShaderPropertyType.Float)
#endif
			)
#endif
				uvChannel = mat.GetFloat(uvProp);



			if (offset == Vector2.zero && scale == Vector2.one && rotation == 0)
			{
				var checkForName = texName + "_ST";
				// Debug.Log("Checking for property: " + checkForName + " : " + mat.HasProperty(checkForName) + " == " + (mat.HasProperty(checkForName) ? mat.GetVector(checkForName) : "null"));
				var textureHasTilingOffset = mat.HasProperty(checkForName);

#if UNITY_2019_1_OR_NEWER
				// turns out we have to check extra hard if that property actually exists
				// the material ALWAYS says true for mat.HasProperty(someTex_ST) when someTex is defined and doesn't have [NoTextureScale] attribute
				if (textureHasTilingOffset)
				{
					if (!CheckForPropertyInShader(mat.shader, checkForName, ShaderPropertyType.Vector))
						textureHasTilingOffset = false;
				}
#endif

				if (textureHasTilingOffset)
				{
					// ignore, texture has explicit _ST property
				}
				else if (TryGetTextureTransform(mat, out var baseColorScale, out var baseColorOffset,
					         out var baseColorRotation, UnityMaterialProperties.BaseColorTexture))
				{
					offset = baseColorOffset;
					scale = baseColorScale;
					rotation = baseColorRotation;
				}
				else
				{
					offset = Vector2.zero;
					scale = Vector2.one;
				}
			}

			if (_root.ExtensionsUsed == null)
			{
				_root.ExtensionsUsed = new List<string>(
					new[] { ExtTextureTransformExtensionFactory.EXTENSION_NAME }
				);
			}
			else if (!_root.ExtensionsUsed.Contains(ExtTextureTransformExtensionFactory.EXTENSION_NAME))
			{
				_root.ExtensionsUsed.Add(ExtTextureTransformExtensionFactory.EXTENSION_NAME);
			}
			
			if (def.Extensions == null)
				def.Extensions = new Dictionary<string, IExtension>();

			def.Extensions[ExtTextureTransformExtensionFactory.EXTENSION_NAME] = new ExtTextureTransformExtension(
				new GLTF.Math.Vector2(offset.x, 1 - offset.y - scale.y),
				rotation,
				new GLTF.Math.Vector2(scale.x, scale.y),
				 (int)uvChannel
			);
		}

		public NormalTextureInfo ExportNormalTextureInfo(
			Texture texture,
			string textureSlot,
			Material material)
		{
			const string normalMapFormatIsXYZ = "_NormalMapFormatXYZ";
			
			var info = new NormalTextureInfo();
			TextureExportSettings exportSettings = default;

#if UNITY_2021_1_OR_NEWER
			if (material.HasFloat(normalMapFormatIsXYZ))
#else
			if (material.HasProperty(normalMapFormatIsXYZ)
#if UNITY_2019_1_OR_NEWER
				&& CheckForPropertyInShader(material.shader, normalMapFormatIsXYZ, ShaderPropertyType.Float)
#endif
			)
#endif			
			if (material.GetFloat(normalMapFormatIsXYZ) >= 1)
			{
				exportSettings.linear = true;
				exportSettings.isValid = true;
				exportSettings.alphaMode = TextureExportSettings.AlphaMode.Never;
				exportSettings.conversion = TextureExportSettings.Conversion.None;
			}
			
			info.Index = ExportTexture(texture, textureSlot, exportSettings);

			if (TryGetFloatFromMaterial(material, out var nScale, UnityMaterialProperties.NormalScale))
				info.Scale = nScale;
			
			return info;
		}

		private OcclusionTextureInfo ExportOcclusionTextureInfo(
			Texture texture,
			string textureSlot,
			Material material, TextureId sharedTextureId = null)
		{
			var info = new OcclusionTextureInfo();

			if (sharedTextureId != null)
				info.Index = sharedTextureId;
			else
				info.Index = ExportTexture(texture, textureSlot);

			if (TryGetFloatFromMaterial(material, out var occlStrength, UnityMaterialProperties.OcclusionStrength))
			{
				info.Strength = occlStrength;
			}
			
			return info;
		}

		public PbrMetallicRoughness ExportPBRMetallicRoughness(Material material)
		{
			var pbr = new PbrMetallicRoughness() { MetallicFactor = 0, RoughnessFactor = 1.0f };
			var isGltfPbrMetallicRoughnessShader = material.shader.name.Equals("GLTF/PbrMetallicRoughness", StringComparison.Ordinal);
			var isGlTFastShader = material.shader.name.Equals("glTF/PbrMetallicRoughness", StringComparison.Ordinal);

			if (TryGetColorFromMaterial(material, out var bColor, UnityMaterialProperties.BaseColor))
			{
				pbr.BaseColorFactor = bColor.ToNumericsColorLinear();
			}

            if (material.HasProperty("_TintColor")) //particles use _TintColor instead of _Color
            {
                float white = 1;
                if (material.HasProperty("_Color"))
                {
                    var c = material.GetColor("_Color");
                    white = (c.r + c.g + c.b) / 3.0f; //multiply alpha by overall whiteness of TintColor
                }

                pbr.BaseColorFactor = (material.GetColor("_TintColor") * white).ToNumericsColorLinear() ;
            }

            if (TryGetTextureFromMaterial(material, out var mainTex, out var mainTexPropertyName, UnityMaterialProperties.BaseColorTexture))
            {
	            //TODO if additive particle, render black into alpha
				// TODO use private Material.GetFirstPropertyNameIdByAttribute here, supported from 2020.1+

				if (mainTex)
				{
					pbr.BaseColorTexture = ExportTextureInfo(mainTex, TextureMapType.BaseColor);
					ExportTextureTransform(pbr.BaseColorTexture, material, mainTexPropertyName);
					if (TryGetTextureUVChannel(material, out var uvChannel, UnityMaterialProperties.BaseColorTexture))
						pbr.BaseColorTexture.TexCoord = uvChannel;
				}
			}

            var ignoreMetallicFactor = (material.IsKeywordEnabled("_METALLICGLOSSMAP") || material.IsKeywordEnabled("_METALLICSPECGLOSSMAP")) && !isGltfPbrMetallicRoughnessShader && !isGlTFastShader;

            if (!ignoreMetallicFactor && TryGetFloatFromMaterial(material, out var mFactor, UnityMaterialProperties.MetallicFactor))
            {
	            pbr.MetallicFactor = mFactor;
            }
            
            var needToBakeRoughnessIntoTexture = false;
            float roughnessMultiplier = 1f;
            bool occlusionGetBakedIntoMetallicRoughness = false;

            if (TryGetFloatFromMaterial(material, out var rFactor, UnityMaterialProperties.RoughnessFactor))
			{
	            pbr.RoughnessFactor = rFactor;
			}
			else if (material.HasProperty("_Glossiness") || material.HasProperty("_Smoothness"))
			{
				var smoothnessPropertyName = material.HasProperty("_Smoothness") ? "_Smoothness" : "_Glossiness";
				var metallicGlossMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
				float smoothness = material.GetFloat(smoothnessPropertyName);

				// legacy workaround: the UnityGLTF shaders misuse "_Glossiness" as roughness but don't have a keyword for it.
				if (isGltfPbrMetallicRoughnessShader)
					smoothness = 1 - smoothness;
				if (metallicGlossMap && material.HasProperty("_GlossMapScale") && material.IsKeywordEnabled("_METALLICGLOSSMAP"))
					smoothness = material.GetFloat("_GlossMapScale");

				var hasMetallicRoughnessMap =
					(material.HasProperty("metallicRoughnessTexture") && material.GetTexture("metallicRoughnessTexture")) ||
					(material.HasProperty("_MetallicRoughnessTexture") && material.GetTexture("_MetallicRoughnessTexture")) ||
					(material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap"));

				if (!hasMetallicRoughnessMap)
					pbr.RoughnessFactor = 1 - smoothness;
				else
				{
					needToBakeRoughnessIntoTexture = true;
					roughnessMultiplier = 1 - smoothness;
					pbr.RoughnessFactor = 1;
				}
			}

            if (TryGetTextureFromMaterial(material, out var mTex, out var metallicTexPropertyName, UnityMaterialProperties.MetallicRoughnessTexture))
			{
				if (mTex)
				{
					pbr.MetallicRoughnessTexture = ExportTextureInfo(mTex, TextureMapType.MetallicRoughness);
					ExportTextureTransform(pbr.MetallicRoughnessTexture, material, metallicTexPropertyName);
				}
			}
			else if (material.HasProperty("_MetallicGlossMap"))
			{
				var mrTex = material.GetTexture("_MetallicGlossMap");

				if (mrTex)
				{
					Texture occlusionTex;
					if (material.HasProperty("_OcclusionMap"))
					{
						occlusionTex = material.GetTexture("_OcclusionMap");
						if (occlusionTex != null && occlusionTex == mrTex)
						{
							occlusionGetBakedIntoMetallicRoughness = true;
						}
					}
					
					var conversion = GetExportSettingsForSlot((isGltfPbrMetallicRoughnessShader || isGlTFastShader) ? TextureMapType.Linear : TextureMapType.MetallicGloss);
					if (needToBakeRoughnessIntoTexture)
					{
						conversion = new TextureExportSettings(conversion);
						conversion.smoothnessRangeMax = 1 - roughnessMultiplier;
					}
					
					if (occlusionGetBakedIntoMetallicRoughness)
					{
						conversion.conversion = TextureExportSettings.Conversion.MetalGlossOcclusionChannelSwap;
						_occlusionBakedTextures.Add(material);
					}
					
					pbr.MetallicRoughnessTexture = ExportTextureInfo(mrTex, TextureMapType.MetallicRoughness, conversion);
					// in the Standard shader, _METALLICGLOSSMAP replaces _Metallic and so we need to set the multiplier to 1;
					// that's not true for the gltf shaders though, so we keep the value there.
					if (ignoreMetallicFactor)
						pbr.MetallicFactor = 1.0f;
					ExportTextureTransform(pbr.MetallicRoughnessTexture, material, "_MetallicGlossMap");
				}
			}
			else if(material.HasProperty("_MaskMap"))
			{
				var mrTex = material.GetTexture("_MaskMap");

				if (mrTex)
				{
					// bake remapping into texture during export
					var conversion = GetExportSettingsForSlot(TextureMapType.MetallicGloss);

					conversion.metallicRangeMin = material.GetFloat("_MetallicRemapMin");
					conversion.metallicRangeMax = material.GetFloat("_MetallicRemapMax");
					conversion.smoothnessRangeMin = material.GetFloat("_SmoothnessRemapMin");
					conversion.smoothnessRangeMax = material.GetFloat("_SmoothnessRemapMax");
					conversion.occlusionRangeMin = material.GetFloat("_AORemapMin");
					conversion.occlusionRangeMax = material.GetFloat("_AORemapMax");
					
					conversion.conversion = TextureExportSettings.Conversion.MetalGlossOcclusionChannelSwap;
					_occlusionBakedTextures.Add(material);

					// set factors to 1 because of baked values
					pbr.MetallicFactor = 1f;
					pbr.RoughnessFactor = 1f;

					pbr.MetallicRoughnessTexture = ExportTextureInfo(mrTex, TextureMapType.MetallicRoughness, conversion);

					ExportTextureTransform(pbr.MetallicRoughnessTexture, material, "_MaskMap");
				}
			}

			return pbr;
		}

		public void ExportUnlit(GLTFMaterial def, Material material){

			const string extname = KHR_MaterialsUnlitExtensionFactory.EXTENSION_NAME;
			DeclareExtensionUsage( extname, true );
			def.AddExtension( extname, new KHR_MaterialsUnlitExtension());

			var pbr = new PbrMetallicRoughness();

			if (TryGetColorFromMaterial(material, out var bColor, UnityMaterialProperties.BaseColor))
			{
				pbr.BaseColorFactor = bColor.ToNumericsColorLinear();
			}

			if (TryGetTextureFromMaterial(material, out var bColTex, out var bColTexProp, UnityMaterialProperties.BaseColorTexture))
			{
				if (bColTex)
				{
					pbr.BaseColorTexture = ExportTextureInfo(bColTex, TextureMapType.BaseColor);
					ExportTextureTransform(pbr.BaseColorTexture, material, bColTexProp);
				}
			}
			
			def.PbrMetallicRoughness = pbr;
		}

		private void ExportPBRSpecularGlossiness(GLTFMaterial material, Material materialObj)
		{
			if (_root.ExtensionsUsed == null)
			{
				_root.ExtensionsUsed = new List<string>(new[] { "KHR_materials_pbrSpecularGlossiness" });
			}
			else if (!_root.ExtensionsUsed.Contains("KHR_materials_pbrSpecularGlossiness"))
			{
				_root.ExtensionsUsed.Add("KHR_materials_pbrSpecularGlossiness");
			}
			
			if (material.Extensions == null)
			{
				material.Extensions = new Dictionary<string, IExtension>();
			}

			GLTF.Math.Color diffuseFactor = KHR_materials_pbrSpecularGlossinessExtension.DIFFUSE_FACTOR_DEFAULT;
			TextureInfo diffuseTexture = KHR_materials_pbrSpecularGlossinessExtension.DIFFUSE_TEXTURE_DEFAULT;
			GLTF.Math.Vector3 specularFactor = KHR_materials_pbrSpecularGlossinessExtension.SPEC_FACTOR_DEFAULT;
			double glossinessFactor = KHR_materials_pbrSpecularGlossinessExtension.GLOSS_FACTOR_DEFAULT;
			TextureInfo specularGlossinessTexture = KHR_materials_pbrSpecularGlossinessExtension.SPECULAR_GLOSSINESS_TEXTURE_DEFAULT;

			if (TryGetColorFromMaterial(materialObj, out var bColor, UnityMaterialProperties.BaseColor))
			{
				diffuseFactor = bColor.ToNumericsColorLinear();
			}

			if (TryGetTextureFromMaterial(materialObj, out var bColTex, out var bColTexProp, UnityMaterialProperties.BaseColorTexture))
			{
				if (bColTex != null)
				{
					diffuseTexture = ExportTextureInfo(bColTex, TextureMapType.BaseColor);
					ExportTextureTransform(diffuseTexture, materialObj, bColTexProp);
				}
			}
			
			if (materialObj.HasProperty("_SpecColor"))
			{
				var specGlossMap = materialObj.GetTexture("_SpecGlossMap");
				if (specGlossMap == null)
				{
					var specColor = materialObj.GetColor("_SpecColor").ToNumericsColorLinear();
					specularFactor = new GLTF.Math.Vector3(specColor.R, specColor.G, specColor.B);
				}
			}

			if (materialObj.HasProperty("_Glossiness"))
			{
				var specGlossMap = materialObj.GetTexture("_SpecGlossMap");
				if (specGlossMap == null)
				{
					glossinessFactor = materialObj.GetFloat("_Glossiness");
				}
			}

			if (materialObj.HasProperty("_SpecGlossMap"))
			{
				var mgTex = materialObj.GetTexture("_SpecGlossMap");

				if (mgTex)
				{
					specularGlossinessTexture = ExportTextureInfo(mgTex, TextureMapType.SpecGloss);
					ExportTextureTransform(specularGlossinessTexture, materialObj, "_SpecGlossMap");
				}
			}

			material.Extensions[KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME] = new KHR_materials_pbrSpecularGlossinessExtension(
				diffuseFactor,
				diffuseTexture,
				specularFactor,
				glossinessFactor,
				specularGlossinessTexture
			);
		}

		private MaterialCommonConstant ExportCommonConstant(Material materialObj)
		{
			if (_root.ExtensionsUsed == null)
			{
				_root.ExtensionsUsed = new List<string>(new[] { "KHR_materials_common" });
			}
			else if (!_root.ExtensionsUsed.Contains("KHR_materials_common"))
			{
				_root.ExtensionsUsed.Add("KHR_materials_common");
			}
			
			var constant = new MaterialCommonConstant();

			if (materialObj.HasProperty("_AmbientFactor"))
			{
				constant.AmbientFactor = materialObj.GetColor("_AmbientFactor").ToNumericsColorRaw();
			}

			if (materialObj.HasProperty("_LightMap"))
			{
				var lmTex = materialObj.GetTexture("_LightMap");

				if (lmTex)
				{
					constant.LightmapTexture = ExportTextureInfo(lmTex, TextureMapType.Linear);
					ExportTextureTransform(constant.LightmapTexture, materialObj, "_LightMap");
				}

			}

			if (materialObj.HasProperty("_LightFactor"))
			{
				constant.LightmapFactor = materialObj.GetColor("_LightFactor").ToNumericsColorRaw();
			}

			return constant;
		}

		// TODO make internal
		public Texture GetSourceTextureForExportedTexture(GLTFTexture exported)
		{
			var textureIndex = _root.Textures.FindIndex(x => x == exported);
			return _textures[textureIndex].Texture;
		}

		// from HDRP's HDUtils.ConvertHDRColorToLDR
		internal static void ConvertHDRColorToLDR(Color hdr, out Color ldr, out float intensity)
		{
			// specifies the max byte value to use when decomposing a float color into bytes with exposure
			// this is the value used by Photoshop
			const float k_MaxByteForOverexposedColor = 191;

			hdr.a = 1.0f;
			ldr = hdr;
			intensity = 1.0f;

			var maxColorComponent = hdr.maxColorComponent;
			if (maxColorComponent != 0f)
			{
				// calibrate exposure to the max float color component
				var scaleFactor = k_MaxByteForOverexposedColor / maxColorComponent;

				ldr.r = Mathf.Min(k_MaxByteForOverexposedColor, scaleFactor * hdr.r) / 255f;
				ldr.g = Mathf.Min(k_MaxByteForOverexposedColor, scaleFactor * hdr.g) / 255f;
				ldr.b = Mathf.Min(k_MaxByteForOverexposedColor, scaleFactor * hdr.b) / 255f;

				intensity = 255f / scaleFactor;
			}
		}
	}
}
