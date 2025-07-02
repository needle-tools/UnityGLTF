using System;
using System.Collections.Generic;
using System.Linq;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF.Plugins;

namespace UnityGLTF
{
    public class MaterialBakerExportContext : GLTFExportPluginContext
    {
        private ExportContext _context;

        // public override bool BeforeMaterialExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Material material, GLTFMaterial materialNode)
        // {
        //     
        // }
        //
        // public override void AfterMaterialExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Material material, GLTFMaterial materialNode)
        // {
        //     base.AfterMaterialExport(exporter, gltfRoot, material, materialNode);
        // }

        private List<MaterialBakerComponent> switchBack = new List<MaterialBakerComponent>();
        
        public MaterialBakerExportContext(ExportContext context)
        {
            _context = context;
        }

        public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            var bakeComponents = exporter.RootTransforms.SelectMany(t =>
                t.GetComponentsInChildren<MaterialBakerComponent>().Where(c => c.exportBakedMaterials)).ToList();

            int index = 0;
            foreach (var bakeComponent in bakeComponents)
            {
                var renderer = bakeComponent.GetComponent<Renderer>();

                if (!bakeComponent.OriginalMaterialActive)
                    switchBack.Add(bakeComponent);

                if (bakeComponent.BakeSettingsChanged || !bakeComponent.HasBakedMaterials)
                {
                    // Add unity progress bar
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.DisplayProgressBar("Baking Materials",
                        $"Baking materials for {renderer.name}", (float)(index++) / (float)bakeComponents.Count);
#endif
                    bakeComponent.Bake();

#if UNITY_EDITOR
                    UnityEditor.EditorUtility.ClearProgressBar();
#endif
                }
                else
                    bakeComponent.SwitchToBakedMaterial();
            }

        }

        public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            foreach (var bakeComponent in switchBack)
            {
                bakeComponent.SwitchToOriginalMaterial();
            }
        }
        
    }

}