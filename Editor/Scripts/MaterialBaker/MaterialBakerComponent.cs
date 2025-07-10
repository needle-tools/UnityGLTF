using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityGLTF
{
    [AddComponentMenu("GLTF/Material Baker")]
    [RequireComponent(typeof(Renderer))]
    public class MaterialBakerComponent : MonoBehaviour
    {
        [Tooltip("When exporting GLTF, export baked materials instead of original materials. Material Baker Export Plugin must be enabled in GLTF Export Settings.\nWhen this Renderer is not already baked, the plugin will be bake this on export")]
        public bool exportBakedMaterials = true;
        
        [Header("Baking Settings")]
        public BakeMode bakeMode = BakeMode.TextureSpace;
        public TextureResolution resolution = new TextureResolution(1024, 1024);
        public Vector2 textureTiling = new Vector2(1, 1);
        
        [SerializeField, HideInInspector] private Material[] lastBakedMaterials = null;
        [SerializeField, HideInInspector] private Texture[] lastBakedTextures = null;
        [SerializeField, HideInInspector] private Material[] orgMaterials = null;
        [SerializeField, HideInInspector] private BakeSettings lastBakeSettings = null;

        public bool HasBakedMaterials => lastBakedMaterials != null && lastBakedMaterials.Length > 0;
        public bool BakeSettingsChanged => !BakeSettings.Equals(lastBakeSettings);
        public BakeSettings BakeSettings => new BakeSettings { resolution = resolution, bakeMode = bakeMode, textureTiling = textureTiling };

        public bool OriginalMaterialActive
        {
            get
            {
                if (orgMaterials == null) return true;
                var r = GetComponent<Renderer>();
                return r.sharedMaterials.SequenceEqual(orgMaterials);
            }
        }

        private bool HasRendererNewMaterials()
        {
            if (lastBakedMaterials == null)
                return false;
            
            var r = GetComponent<Renderer>();
            var cntOrgMaterials = orgMaterials?.Length ?? 0;
            var cntSharedMaterials = r.sharedMaterials?.Length ?? 0;
            var cntBakedMaterials = lastBakedMaterials?.Length ?? 0;

            if (cntSharedMaterials != cntOrgMaterials && cntSharedMaterials != cntBakedMaterials)
                return true;
            
            if (cntSharedMaterials == cntOrgMaterials && r.sharedMaterials != null)
            {
                if (r.sharedMaterials.SequenceEqual(orgMaterials))
                    return false;
            }

            if (cntSharedMaterials == cntBakedMaterials && r.sharedMaterials != null)
            {
                if (r.sharedMaterials.SequenceEqual(lastBakedMaterials))
                    return false;
            }

            return true;
        }

        private void CheckRendererMaterials()
        {
            if (HasRendererNewMaterials())
            {
                DestroyLastBakedMaterials();
            } 
        }
        
#if UNITY_EDITOR
        public void DestroyLastBakedMaterials()
        {
            SwitchToOriginalMaterial();
            if (lastBakedMaterials != null)
            {
                foreach (var mat in lastBakedMaterials)
                {
                    if (mat != null)
                    {
                        if (orgMaterials.Contains(mat))
                            continue;

                        var matPath = AssetDatabase.GetAssetPath(mat);
                        AssetDatabase.DeleteAsset(matPath);
                    }
                }
                lastBakedMaterials = null;
            }
            
            if (lastBakedTextures != null)
            {
                foreach (var tex in lastBakedTextures)
                {
                    if (tex != null)
                    {
                        var texPath = AssetDatabase.GetAssetPath(tex);
                        AssetDatabase.DeleteAsset(texPath);
                    }
                }
                lastBakedTextures = null;
            }
        }
        
        public void Bake()
        {
            DestroyLastBakedMaterials();
            
            var r = GetComponent<Renderer>();
            orgMaterials = r.sharedMaterials;
            
            var maps = MaterialBaker.Bake(r, BakeSettings);

            var bakedMaterials = new List<Material>();
            var bakedTextures = new List<Texture>();

            int index = 0;
            foreach (var map in maps)
            {
                if (map.ignore)
                {
                    bakedMaterials.Add(map.forMaterial);
                    index++;
                    continue;
                }
                var newMaterial = ChannelExporter.SaveMaps(map, bakeMode == BakeMode.UV1 ? 1 : 0);
                bakedMaterials.Add(newMaterial);

                var textureProperties = newMaterial.GetTexturePropertyNames();
                
                bakedTextures.AddRange( textureProperties.Select(newMaterial.GetTexture).Where(t => t != null));
                index++;
            }
            lastBakedMaterials = bakedMaterials.ToArray();
            lastBakedTextures = bakedTextures.Distinct().ToArray();
            lastBakeSettings = BakeSettings;
            SwitchToBakedMaterial();
        }
        
        public void SwitchToBakedMaterial()
        {
            if (lastBakedMaterials == null || lastBakedMaterials.Length == 0)
                return;
            var r = GetComponent<Renderer>();
            r.sharedMaterials = lastBakedMaterials;
        }
        
        public void SwitchToOriginalMaterial()
        {
            if (orgMaterials == null || orgMaterials.Length == 0)
                return;
            
            if (lastBakedMaterials == null || lastBakedMaterials.Length == 0)
                return;
            
            var r = GetComponent<Renderer>();
            r.sharedMaterials = orgMaterials;
        }
#endif
        
#if UNITY_EDITOR
        [CanEditMultipleObjects]
        [CustomEditor(typeof(MaterialBakerComponent))]
        public class Inspector : UnityEditor.Editor
        {
            private static bool foldOutMaterials = false;
            
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var anyRequiresRebake = false;
                var anyHasBakedMaterials = false;

                var bakers = targets.Select(x => x as MaterialBakerComponent).ToList();
                
                foreach (var baker in bakers)
                {
                    if (baker.HasRendererNewMaterials())
                        EditorApplication.delayCall += baker.DestroyLastBakedMaterials;
                    
                    var requireRebake = baker.HasBakedMaterials && baker.BakeSettingsChanged;
                    anyRequiresRebake |= requireRebake;
                    anyHasBakedMaterials |= baker.HasBakedMaterials;
                }
                
                GUI.color = anyRequiresRebake ? Color.yellow : (anyHasBakedMaterials ? Color.white : Color.green);
                if (GUILayout.Button(anyRequiresRebake ? "Rebake" : "Bake", GUILayout.Height(30f)))
                {
                    foreach (var target in targets)
                    {
                        var baker = target as MaterialBakerComponent;
                        baker?.Bake();
                    }
                }
                GUI.color = Color.white;

                if (anyHasBakedMaterials)
                {
                    GUILayout.Space(10f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Switching:");
                    var anyOriginalActive = bakers
                        .Where(x => x.HasBakedMaterials)
                        .Any(b => b.OriginalMaterialActive);
                    
                    GUI.color = anyOriginalActive ? Color.green : Color.white;
                    if (GUILayout.Button("Original Materials"))
                    {
                        foreach (var baker in bakers)
                            baker.SwitchToOriginalMaterial();
                    }
                    GUI.color = !anyOriginalActive ? Color.green : Color.white;
                    if (GUILayout.Button("Baked Materials"))
                    {
                        foreach (var baker in bakers)
                        {
                            if (!baker.HasBakedMaterials)
                                baker.Bake();
                            else
                                baker.SwitchToBakedMaterial();
                        }
                    }
                    GUI.color = Color.white;

                    EditorGUILayout.EndHorizontal();
                    
                    GUILayout.Space(10f);
                    if (GUILayout.Button("Destroy Last Baked Materials"))
                    {
                        foreach (var baker in bakers)
                            baker.DestroyLastBakedMaterials();
                    }
                    
                    GUILayout.Space(10f);
                    foldOutMaterials = EditorGUILayout.Foldout(foldOutMaterials, "Materials");
                    if (foldOutMaterials && targets.Length == 1 && target is MaterialBakerComponent bakerComponent)
                    {
                        // Display the last bake settings
                        EditorGUILayout.LabelField("Last Bake Settings:");
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Bake Mode", bakerComponent.lastBakeSettings.bakeMode.ToString());
                        EditorGUILayout.LabelField("Texture Resolution", $"{bakerComponent.lastBakeSettings.resolution.width}x{bakerComponent.lastBakeSettings.resolution.height}");
                        EditorGUI.indentLevel--;
                        GUILayout.Space(5);
                        // Get width of the editor window
                        var width = EditorGUIUtility.currentViewWidth;
                        var widthPerColumn = width / 3f;
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Baked Materials", GUILayout.Width(widthPerColumn));
                        EditorGUILayout.LabelField("Baked Textures", GUILayout.Width(widthPerColumn));
                        EditorGUILayout.LabelField("Original Materials", GUILayout.Width(widthPerColumn));
                        GUILayout.EndHorizontal();

                        
                        GUILayout.BeginHorizontal();
                        if (bakerComponent.lastBakedMaterials != null && bakerComponent.lastBakedMaterials.Length > 0)
                        {
                            GUILayout.BeginVertical(GUILayout.Width(widthPerColumn) );
                            foreach (var mat in bakerComponent.lastBakedMaterials.Where( mat => !bakerComponent.orgMaterials.Contains(mat)))
                            {
                                EditorGUILayout.ObjectField(mat, typeof(Material), false);
                            }
                            GUILayout.EndVertical();
                        }
                        else
                        {
                            GUILayout.Label("No baked materials found.");
                        }
                        
                        if (bakerComponent.lastBakedTextures != null && bakerComponent.lastBakedTextures.Length > 0)
                        {
                            GUILayout.BeginVertical(GUILayout.Width(widthPerColumn));
                            foreach (var tex in bakerComponent.lastBakedTextures)
                            {
                                EditorGUILayout.ObjectField(tex, typeof(Texture), false);
                            }
                            GUILayout.EndVertical();
                        }
                        else
                        {
                            GUILayout.Label("No baked textures found.");
                        }
                        
                        if (bakerComponent.orgMaterials != null && bakerComponent.orgMaterials.Length > 0)
                        {
                            GUILayout.BeginVertical(GUILayout.Width(widthPerColumn));
                            foreach (var mat in bakerComponent.orgMaterials)
                            {
                                EditorGUILayout.ObjectField(mat, typeof(Material), false);
                            }
                            GUILayout.EndVertical();

                        }
                        else
                        {
                            GUILayout.Label("No orig. materials found.");
                        }
                        
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }
#endif
    }
}