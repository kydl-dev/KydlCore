using KydlCore.Common;
using KydlCore.Core;
using KydlCore.Features.Hub.Infrastructure;
using KydlCore.Features.Items.Infrastructure;

namespace KydlCore.Bootstrap;

/// <summary>
/// KydlCore's composition root (spec "Bootstrap"): the shared services and the features in the <see cref="FeatureRegistry"/>. KydlCore's features
/// are ALWAYS on (MIGRATION_DECISIONS B12: no EnableAll, nothing it does by default can be switched off), so the registry starts them once.
/// </summary>
internal sealed class ModInstaller
{
    internal static ModInstaller Current { get; private set; }
    internal DependencyRegistry Deps { get; }
    internal FeatureRegistry Features { get; }

    internal ModInstaller()
    {
        Deps = new DependencyRegistry();
        Features = new FeatureRegistry(Deps.Trace);
    }

    /// Expected outcome: true when every feature started.
    internal bool Install()
    {
        Current = this;
        Add(new ItemsFeature());          // first: the hub's KY-Items tab follows the registry
        Add(new HubFeature());
        OperationResponse<int> r = Features.Refresh();
        if (!r.IsSuccess) ModLog.Error("ModInstaller: " + r.Reason);
        ModLog.Verbose("ModInstaller: " + Features.Count + " feature(s) - " + r);
        return r.IsSuccess;
    }

    /// Plugin unload. Expected outcome: true when every feature stopped.
    internal bool Uninstall()
    {
        OperationResponse<int> r = Features.StopAll();
        if (!r.IsSuccess) ModLog.Warn("ModInstaller: " + r.Reason);
        if (Current == this) Current = null;
        return r.IsSuccess;
    }

    void Add(IFeature feature)
    {
        OperationResponse<int> r = Features.Add(feature);
        if (!r.IsSuccess) ModLog.Error("ModInstaller: feature not added - " + r.Reason);
    }
}
