using System;
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

        public MaterialBakerExportContext(ExportContext context)
        {
            _context = context;
        }
        
        public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
           var bakeComponents = exporter.RootTransforms.SelectMany(t => t.GetComponentsInChildren<MaterialBakerComponent>()).ToList();   
           
           foreach (var bakeComponent in bakeComponents)
           {
               var renderer = bakeComponent.GetComponent<Renderer>();
               var maps = MaterialBaker.Bake(renderer, bakeComponent.BakeSettings);

               foreach (var map in maps)
               {
                   var newMaterial = ChannelExporter.SaveMaps(map, bakeComponent.BakeSettings.bakeMode == MaterialBaker.BakeMode.UV1 ? 1: 0);
                   var materialId = exporter.ExportMaterial(newMaterial);
                   
               }
               
           }
        }
        
    }

}