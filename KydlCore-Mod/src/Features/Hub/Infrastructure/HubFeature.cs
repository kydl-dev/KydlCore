using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using KydlCore.Core;
using KydlCore.Presentation.Hub.Registration;

namespace KydlCore.Features.Hub.Infrastructure
{
    /// <summary>
    /// The KY hub (F6) and KydlCore's own tabs (Settings, KY-Items). Always on (MIGRATION_DECISIONS B12). Start registers KydlCore's tabs; Stop (plugin
    /// unload only) closes every window. The hub's public API is <see cref="KyHub"/> (namespace KydlCore, used by every mod).
    /// </summary>
    internal sealed class HubFeature : IFeature
    {
        internal const string FeatureId = "hub";
        const bool AlwaysOn = true;

        public string Id { get { return FeatureId; } }
        public bool IsEnabled() { return AlwaysOn; }

        public bool Start()
        {
            bool tabs = KyItemsTab.Init();
            ModLog.Verbose("HubFeature: started (KydlCore tabs " + tabs + ")");
            return tabs;
        }

        public bool Stop()
        {
            HubHost.Close("KydlCore stopping");
            KyWindows.CloseAll("KydlCore stopping");
            bool closed = !KyHub.IsOpen;
            ModLog.Verbose("HubFeature: stopped (hub closed " + closed + ")");
            return closed;
        }
    }
}
