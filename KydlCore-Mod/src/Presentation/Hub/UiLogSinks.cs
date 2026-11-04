using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using KydlCore.Common;
using KydlCore.Features.Hub.Domain;
using KydlCore.Ui;

namespace KydlCore.Presentation.Hub;

/// <summary>
/// The log sinks of the shared UI kit (D-071). KydlCore draws every mod's tab, so the kit's lines would all be written by KydlCore; here they get
/// the mod's name in front (`[ChestDecor] Ui: button 'X' pressed`) and a debug line follows the OWNING mod's `[DebugLog] Enable` (the rules are
/// in UiLogRoute). Game glue (BepInEx plugin list, config): tested in game, not by unit tests. These methods are the log itself, so they write
/// no debug line of their own (it would log about logging).
/// </summary>
internal static class UiLogSinks
{
    /// Called once from Plugin.Awake. Expected outcome: the three sinks are set.
    internal static bool Install()
    {
        UiLog.VerboseSink = Verbose;
        UiLog.WarnSink = Warn;
        UiLog.ErrorSink = Error;
        ModLog.Verbose("UiLogSinks: installed (UI lines carry the mod name; a mod's debug line follows that mod's DebugLog switch)");
        return UiLog.VerboseSink != null && UiLog.WarnSink != null && UiLog.ErrorSink != null;
    }

    static void Verbose(string message)
    {
        string owner = UiLog.Owner;
        if (!UiLogRoute.ShowVerbose(owner, OwnerDebugOn(owner), ModLog.DebugOn)) return;
        ModLog.Info(UiLogRoute.Tag(NameOf(owner), message));        // Info: the owner's switch already said yes, ModLog.Verbose would ask KydlCore's
    }

    static void Warn(string message) { ModLog.Warn(UiLogRoute.Tag(NameOf(UiLog.Owner), message)); }

    static void Error(string message) { ModLog.Error(UiLogRoute.Tag(NameOf(UiLog.Owner), message)); }

    /// The plugin's name, or null (no owner, or a GUID that is not loaded).
    static string NameOf(string guid)
    {
        PluginInfo info;
        if (guid == null || !Chainloader.PluginInfos.TryGetValue(guid, out info)) return null;
        return info.Metadata.Name;
    }

    /// The owner's `[DebugLog] Enable` (every mod has it, PROJECT_RULES 11); false when the mod is not loaded or has no such entry.
    static bool OwnerDebugOn(string guid)
    {
        PluginInfo info;
        if (guid == null || !Chainloader.PluginInfos.TryGetValue(guid, out info) || info.Instance == null) return false;
        ConfigEntry<bool> entry;
        if (!info.Instance.Config.TryGetEntry<bool>("DebugLog", "Enable", out entry)) return false;
        return entry.Value;
    }
}
