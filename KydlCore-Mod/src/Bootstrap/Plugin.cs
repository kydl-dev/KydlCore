using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using KydlCore.Common;
using KydlCore.Configuration;
using KydlCore.Features.Hub.Infrastructure;
using KydlCore.Presentation.Hub;
using UnityEngine;

namespace KydlCore.Bootstrap;

/// <summary>
/// KydlCore: Shared runtime for all KydlDev mods: one UI root and theme, the KY hub window (F6; the one menu of every KydlDev mod) and the KY-Items category and registry.
/// Entry point. Order in Awake matters: log first, then the config (player settings), then text, then everything that uses them.
/// </summary>
[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
// MIGRATION_DECISIONS "Network compatibility": VersionCheckOnly + Patch - a client with only client-only mods + KydlCore joins a server without
// KydlCore; when both sides have it the versions must match exactly (a server running a gameplay mod always has KydlCore).
[NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Patch)]
public class Plugin : BaseUnityPlugin
{
    // ModGuid never changes after the first release. ModVersion = manifest.json version_number = newest CHANGELOG heading (tests check it).
    public const string ModGuid = "com.kydldev.KydlCore";
    public const string ModName = "KydlCore";
    public const string ModVersion = "1.0.0";
    public const string BundleName = "kydlcore";
    /// UiKit.Owner: the name part of the one UI root object ("Kydl_UI_" + owner).
    internal const string UiOwner = "Root";

    internal static Plugin Instance;
    /// This mod's AssetBundle (null while the mod has none yet: Unity has not built one).
    internal static AssetBundle Bundle;
    ConfigReloader _cfgWatcher;
    ModInstaller _installer;

    void Awake()
    {
        Instance = this;
        ModLog.Init(Logger, ModName, () => ModSettings.DebugLog != null && ModSettings.DebugLog.Value);
        ModLog.Info(ModName + " " + ModVersion + " loaded (dll built " + File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm:ss") + ")");

        // The shared UI kit (KydlCore.Ui) lives in this DLL only (D-040): Core sets its log sinks, its panel owner and the input lock ONCE.
        // A mod moved onto Core never sets them itself (the last writer would take over). Debug lines only with the debug switch on.
        UiLogSinks.Install();                     // UI lines get the owning mod's name and follow its debug switch (D-071)
        global::KydlCore.Ui.UiKit.Owner = UiOwner;               // the one UI panel object is called Kydl_UI_Root

        ModSettings.Bind(Config);
        _cfgWatcher = new ConfigReloader(Config);
        Loc.Register(Ui.UiText.En);       // Ui. = KydlCore.Ui (no using: its sort position depends on the mod name, dotnet format IMPORTS)

        Bundle = LoadBundle();
        // the features (hub, KY-Items) start through the FeatureRegistry (MIGRATION_DECISIONS A4); KydlCore's are always on (B12)
        _installer = new ModInstaller();
        bool installed = _installer.Install();
        ModLog.Verbose("Plugin: UI owner '" + global::KydlCore.Ui.UiKit.Owner + "' (root object Kydl_UI_" + global::KydlCore.Ui.UiKit.Owner + "), input lock " + (UiInputLock.Install(ModGuid) ? "installed" : "NOT installed") + " (the character does not move while a Core window is open)");
        new Harmony(ModGuid).PatchAll(Assembly.GetExecutingAssembly());
        ModLog.Verbose("Plugin: Awake finished (architecture " + Core.Architecture.Tag + ", Harmony patches applied, features " + (installed ? "started" : "had failures") + ", bundle " + (Bundle != null ? "loaded" : "none") + ")");
    }

    // 1) embedded in the DLL (Kydl.Mod.targets embeds Assets\<bundle>; read fully into memory: LoadFromStream breaks once the stream
    //    is disposed), 2) the file next to the DLL (the Gale test profile gets both). No bundle at all is normal for a new mod.
    static AssetBundle LoadBundle()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var res in asm.GetManifestResourceNames())
        {
            if (res != BundleName && !res.EndsWith("." + BundleName)) continue;
            using (var stream = asm.GetManifestResourceStream(res))
            using (var ms = new MemoryStream())
            {
                if (stream != null) stream.CopyTo(ms);
                var b = AssetBundle.LoadFromMemory(ms.ToArray());
                if (b != null) { ModLog.Verbose("Plugin: bundle '" + BundleName + "' loaded from the DLL (" + b.GetAllAssetNames().Length + " asset(s))"); return b; }
                ModLog.Warn("Plugin: the embedded bundle '" + res + "' could not be loaded");
            }
        }
        var path = Path.Combine(Path.GetDirectoryName(asm.Location) ?? "", BundleName);
        if (!File.Exists(path)) { ModLog.Verbose("Plugin: no bundle '" + BundleName + "' (not embedded, not at " + path + ") - fine until the mod has assets"); return null; }
        var fromFile = AssetBundle.LoadFromFile(path);
        if (fromFile == null) ModLog.Warn("Plugin: bundle file " + path + " could not be loaded");
        else ModLog.Verbose("Plugin: bundle loaded from " + path);
        return fromFile;
    }

    void Update()
    {
        if (_cfgWatcher != null) _cfgWatcher.Tick();
        KeyCapture.Tick();                 // first: a key chosen for a setting is not also the hub key or Escape
        HubHost.Tick();
        KyWindows.Tick();                  // Escape closes the front window: the hub or a KyUi window
    }

    void OnDestroy()
    {
        if (_cfgWatcher != null) { _cfgWatcher.Dispose(); _cfgWatcher = null; }
        if (_installer != null) { _installer.Uninstall(); _installer = null; }
        HubHost.Close("plugin destroyed");
        KyWindows.CloseAll("plugin destroyed");
        ModLog.Verbose("Plugin: destroyed");
    }
}
