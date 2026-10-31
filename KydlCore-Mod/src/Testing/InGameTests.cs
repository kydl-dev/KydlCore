using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace KydlCore.Testing
{
    /// <summary>
    /// In-game tests run by the user's Kydl.GameProbe (dev tool, never a dependency: it finds this class by its NAME `InGameTests`).
    ///   Startup_*  run when the main menu is reached;   World_*  run 5 s after a world is loaded; both again on GameProbe's RetestKey (PageDown).
    /// Each method: static, no parameters, returns a text starting with PASS / FAIL / SKIP and the detail with numbers + units,
    /// e.g. "FAIL: coil radius 0.900 m, expected 0.180 m". null or an exception counts as FAIL. Results land in the session report.
    /// Keep them cheap and read-only: they also run in players' games when GameProbe is installed there (it never is for players).
    /// </summary>
    internal static class InGameTests
    {
        /// The player config is bound and the mod is switched on.
        static string Startup_ConfigBound()
        {
            if (ModSettings.EnableAll == null) return "FAIL: ModSettings.Bind did not run (EnableAll is null)";
            return ModSettings.On ? "PASS: EnableAll on, debug log " + (ModSettings.DebugLog != null && ModSettings.DebugLog.Value ? "on" : "off") : "SKIP: EnableAll is off in the config";
        }

        /// PLAN T-022a: is Valheim's font NotoSansSC-Regular among the loaded Font objects (the same lookup UiKit.LoadFonts uses: Font.name, exact)?
        /// At the main menu ...
        static string Startup_FontsLoaded() { return CheckFonts(); }

        /// ... and again once a world is loaded (the game may load fonts lazily, so both moments are recorded).
        static string World_FontsLoaded() { return CheckFonts(); }

        static string CheckFonts()
        {
            var names = new HashSet<string>();
            var related = new List<string>();
            int total = 0;
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (f == null) continue;
                total++;
                names.Add(f.name);
                if (f.name.IndexOf("Noto", System.StringComparison.OrdinalIgnoreCase) >= 0 || f.name.IndexOf("Norse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    related.Add(f.name + (f.dynamic ? " (dynamic)" : " (static)"));
            }
            string detail = total + " Font objects loaded; NotoSansSC-Regular " + Has(names, "NotoSansSC-Regular") + ", NotoSansSC-Thin " + Has(names, "NotoSansSC-Thin")
                          + ", Norse " + Has(names, "Norse") + ", Norsebold " + Has(names, "Norsebold") + ", NotoEmoji-Regular " + Has(names, "NotoEmoji-Regular")
                          + "; Noto / Norse fonts by name: " + (related.Count == 0 ? "none" : string.Join(", ", related.ToArray()));
            ModLog.Verbose("InGameTests: " + detail);
            return names.Contains("NotoSansSC-Regular") ? "PASS: " + detail : "FAIL: NotoSansSC-Regular is not a loaded Font: " + detail;
        }

        static string Has(HashSet<string> names, string name) { return names.Contains(name) ? "yes" : "NO"; }

        /// KC-01 (DESIGN 10): at most one UI root object "Kydl_UI_Root" (two would mean two copies of the kit are running). 0 is fine before the first window.
        static string Startup_KC01_OneUiRoot() { return CheckUiRoot(); }

        static string CheckUiRoot()
        {
            int n = 0;
            foreach (var doc in Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None))
                if (doc != null && doc.gameObject.name == "Kydl_UI_" + Plugin.UiOwner) n++;
            ModLog.Verbose("InGameTests: KC-01 " + n + " Kydl_UI_" + Plugin.UiOwner + " UIDocument(s)");
            return n <= 1 ? "PASS: " + n + " Kydl_UI_" + Plugin.UiOwner + " UIDocument(s)" : "FAIL: " + n + " Kydl_UI_" + Plugin.UiOwner + " UIDocuments, expected at most 1";
        }

        /// KC-02: row 1 of the hub = every loaded plugin whose GUID starts with com.kydldev. (read from the hub model; SKIP while the hub was never opened).
        static string World_KC02_HubRowOne()
        {
            int shown = KyHub.Model.ModCount;
            if (shown == 0) return "SKIP: the hub was not opened yet (its mod list is read when it opens): press F6 once, then PageDown";
            int loaded = 0;
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                if (info != null && info.Metadata != null && Ui.HubModel.IsHubGuid(info.Metadata.GUID)) loaded++;
            ModLog.Check("KC-02 row-1 tabs", shown, loaded, 0, "mods");
            return shown == loaded ? "PASS: " + shown + " mod(s) in row 1 = " + loaded + " loaded com.kydldev.* plugin(s)" : "FAIL: " + shown + " mod(s) in row 1, " + loaded + " loaded com.kydldev.* plugin(s)";
        }

        /// KC-03: every mod in the hub has at least one tab (its own, or the automatic Settings); KydlCore has "settings" and has "ky-items" exactly when the registry has entries.
        static string World_KC03_HubTabs()
        {
            var model = KyHub.Model;
            if (model.ModCount == 0) return "SKIP: the hub was not opened yet: press F6 once, then PageDown";
            foreach (string mod in model.ModGuids())
                if (model.TabIds(mod).Length == 0) return "FAIL: " + mod + " has no tab in row 2";
            var core = new List<string>(model.TabIds(Plugin.ModGuid));
            bool hasItems = core.Contains(KyItemsTab.ItemsId), wantItems = KyItems.All.Count > 0;
            ModLog.Verbose("InGameTests: KC-03 KydlCore tabs " + string.Join(", ", core.ToArray()) + ", registry " + KyItems.All.Count);
            if (!core.Contains(KyItemsTab.SettingsId)) return "FAIL: KydlCore has no 'settings' tab (" + string.Join(", ", core.ToArray()) + ")";
            return hasItems == wantItems ? "PASS: all " + model.ModCount + " mod(s) have a tab; KydlCore tabs " + string.Join(", ", core.ToArray())
                                         : "FAIL: ky-items tab " + (hasItems ? "shown" : "missing") + " but the registry has " + KyItems.All.Count + " entr" + (KyItems.All.Count == 1 ? "y" : "ies");
        }

        /// KC-04: the KY-Items registry has read the game and found at least every prefab Jotunn registered for a com.kydldev.* mod (one per prefab name).
        static string World_KC04_KyItemsCount()
        {
            var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var ci in Jotunn.Utils.ModRegistry.GetItems())
                if (ci != null && ci.ItemPrefab != null && ci.SourceMod != null && KyItems.IsUserGuid(ci.SourceMod.GUID)) names.Add(ci.ItemPrefab.name);
            foreach (var cp in Jotunn.Utils.ModRegistry.GetPieces())
                if (cp != null && cp.PiecePrefab != null && cp.SourceMod != null && KyItems.IsUserGuid(cp.SourceMod.GUID)) names.Add(cp.PiecePrefab.name);
            int have = KyItems.All.Count;
            ModLog.Check("KC-04 KY-Items entries (at least the Jotunn ones)", have, names.Count, 0, "prefabs");
            if (!KyItems.Ready) return "FAIL: the registry is not Ready 5 s after the world loaded";
            foreach (string n in names) if (KyItems.Find(n) == null) return "FAIL: '" + n + "' is registered by Jotunn for a com.kydldev.* mod but is not in the registry";
            return "PASS: " + have + " entr" + (have == 1 ? "y" : "ies") + " (" + names.Count + " from Jotunn); " + CountNoIcon() + " without an icon";
        }

        /// KC-05: KyUi opens and closes a window of a mod (D-072): IsOpen / AnyOpen follow, onClosed runs once, a same-id window replaces the old one.
        static string World_KC05_KyUiWindow()
        {
            const string guid = "com.kydldev.kydlcore.test", id = "kc05";
            if (KyUi.Root == null) return "SKIP: the UI root does not exist yet (open the hub once with F6, then PageDown)";
            bool wasOpen = KyUi.AnyOpen;
            int closed = 0;
            var first = KyUi.Open(guid, id, new UnityEngine.UIElements.VisualElement(), () => closed++);
            if (first == null) return "FAIL: KyUi.Open returned null";
            bool openAfter = KyUi.IsOpen(guid, id) && KyUi.AnyOpen;
            var second = KyUi.Open(guid, id, new UnityEngine.UIElements.VisualElement(), () => closed++);
            bool replaced = closed == 1 && KyUi.IsOpen(guid, id);
            bool refused = KyUi.Open(guid, " ", new UnityEngine.UIElements.VisualElement()) == null;
            second.Close();
            second.Close();
            bool done = !KyUi.IsOpen(guid, id) && closed == 2 && KyUi.AnyOpen == wasOpen;
            ModLog.Check("KC-05 KyUi window", closed, 2, 0, "onClosed calls");
            if (openAfter && replaced && refused && done) return "PASS: window opened, replaced by the same id, closed once; onClosed ran 2 times, any-open back to " + wasOpen;
            return "FAIL: openAfter " + openAfter + ", replaced " + replaced + ", blankIdRefused " + refused + ", closedClean " + done + " (onClosed " + closed + ")";
        }

        static string CountNoIcon()
        {
            int none = 0;
            foreach (var e in KyItems.All) if (e.Icon == null) none++;
            return none.ToString();
        }

        /// The local player exists once a world is loaded (example of a World_ test; replace with the mod's own checks).
        static string World_PlayerPresent()
        {
            var p = Player.m_localPlayer;
            ModLog.Verbose("InGameTests: World_PlayerPresent player " + (p != null ? p.GetPlayerName() : "null"));
            return p != null ? "PASS: " + p.GetPlayerName() + " at " + p.transform.position.ToString("F1") : "FAIL: no local player 5 s after the world loaded";
        }
    }
}
