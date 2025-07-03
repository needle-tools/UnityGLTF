#if HAVE_NEEDLE

using System.Reflection;
using JetBrains.Annotations;
using UnityGLTF;

namespace Needle.Engine.Gltf.UnityGltf
{
    [UsedImplicitly]
    public class MaterialBakerNeedlePlugin : GltfExtensionHandlerBase
    {
        MaterialBakerExportContext materialBaker;
        GLTFSceneExporter gltfExporter;
        
        public override void OnBeforeExport(GltfExportContext context)
        {
            // TODO No access to ExportContext here either
            // var exportHandler = context.Handler as UnityGltfExportHandler;

            gltfExporter = context.Exporter as GLTFSceneExporter;
            var exportContext = gltfExporter?.GetType().GetField("_exportContext", (BindingFlags)(-1))?.GetValue(gltfExporter) as ExportContext;
            if (exportContext != null)
                materialBaker = new MaterialBakerExportContext(exportContext);
            materialBaker?.BeforeSceneExport(gltfExporter, null);
        }

        public override void OnAfterExport(GltfExportContext context)
        {
            materialBaker?.AfterSceneExport(gltfExporter, null);
        }
    }
}

#endif