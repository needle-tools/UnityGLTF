using UnityGLTF.Plugins;

namespace UnityGLTF
{
    public class MaterialBakerExportPlugin : GLTFExportPlugin
    {
        public override string DisplayName
        {
            get => "Material Baker";
        }
        
        public override string Description
        {
            get => "Bakes all materials from Renderers, which has a MaterialBaker component on it.";
        }

        public override bool EnabledByDefault { get; } = false;
        
        public override GLTFExportPluginContext CreateInstance(ExportContext context)
        {
            return new MaterialBakerExportContext(context);
        }
    }
}