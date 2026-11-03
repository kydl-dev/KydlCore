using KydlCore.Common;
using KydlCore.Features.Items.Contracts;
using KydlCore.Core;

namespace KydlCore.Features.Items.Infrastructure
{
    /// <summary>The KY-Items registry (spec 5). Always on (MIGRATION_DECISIONS B12). Start hooks the registry to the game side (Integrations/Jotunn).</summary>
    internal sealed class ItemsFeature : IFeature
    {
        internal const string FeatureId = "ky-items";
        const bool AlwaysOn = true;

        public string Id { get { return FeatureId; } }
        public bool IsEnabled() { return AlwaysOn; }

        public bool Start()
        {
            bool hooked = KyItems.Init();
            ModLog.Verbose("ItemsFeature: started (game side hooked " + hooked + ")");
            return hooked;
        }

        public bool Stop()
        {
            ModLog.Verbose("ItemsFeature: stopped (the registry keeps its entries until the game exits)");
            return KyItems.All != null;
        }
    }
}
