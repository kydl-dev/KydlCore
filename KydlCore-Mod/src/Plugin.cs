using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using Kydl.Common;
using UnityEngine;

namespace KydlCore
{
    /// <summary>
    /// KydlCore: Shared runtime for all KydlDev mods: one UI root and theme, the KY hub window (F6; the one menu of every KydlDev mod) and the KY-Items category and registry.
    /// Entry point. Order in Awake matters: log first, then the config (player settings), then text, then everything that uses them.
    /// </summary>
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        // ModGuid never changes after the first release. ModVersion = manifest.json version_number = newest CHANGELOG heading (tests check it).
        public const string ModGuid = "com.kydldev.KydlCore";
        public const string ModName = "KydlCore";
        public const string ModVersion = "0.1.0";
        public const string BundleName = "kydlcore";

        internal static Plugin Instance;
        /// This mod's AssetBundle (null while the mod has none yet: Unity has not built one).
        internal static AssetBundle Bundle;
        ConfigReloader _cfgWatcher;

        void Awake()
        {
            Instance = this;
            ModLog.Init(Logger, ModName, () => ModSettings.DebugLog != null && ModSettings.DebugLog.Value);
            ModLog.Info(ModName + " " + ModVersion + " loaded (dll built " + File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm:ss") + ")");

            // the shared UI kit (Kydl.Ui) logs through the mod's log: debug lines only with the debug switch on
            Kydl.Ui.UiLog.VerboseSink = s => ModLog.Verbose(s);
            Kydl.Ui.UiLog.WarnSink = s => ModLog.Warn(s);
            Kydl.Ui.UiLog.ErrorSink = s => ModLog.Error(s);
            Kydl.Ui.UiKit.Owner = ModName;

            ModSettings.Bind(Config);
            _cfgWatcher = new ConfigReloader(Config);
            Loc.Register(Ui.UiText.En);       // Ui. = KydlCore.Ui (no using: its sort position depends on the mod name, dotnet format IMPORTS)

            Bundle = LoadBundle();
            UiInputLock.Install(ModGuid);           // the character does not move while one of the mod's menus is open
            new Harmony(ModGuid).PatchAll(Assembly.GetExecutingAssembly());
            ModLog.Verbose("Plugin: Awake finished (Harmony patches applied, mod " + (ModSettings.On ? "on" : "inert: EnableAll is false") + ", bundle " + (Bundle != null ? "loaded" : "none") + ")");
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
        }

        void OnDestroy()
        {
            if (_cfgWatcher != null) { _cfgWatcher.Dispose(); _cfgWatcher = null; }
            ModLog.Verbose("Plugin: destroyed");
        }
    }
}
