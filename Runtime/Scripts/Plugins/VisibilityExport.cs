namespace UnityGLTF.Plugins
{
    public class VisibilityExport : GLTFExportPlugin
    {
        public override string DisplayName => "KHR_visibility";
        public override string Description => "Exports visibility of objects.";
        public override bool AlwaysEnabled => false;
        public override bool EnabledByDefault => false;
        public override GLTFExportPluginContext CreateInstance(ExportContext context)
        {
            // always enabled
            return null;
        }
    }
}