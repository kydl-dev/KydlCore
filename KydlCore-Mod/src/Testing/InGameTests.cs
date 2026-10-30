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

        /// The local player exists once a world is loaded (example of a World_ test; replace with the mod's own checks).
        static string World_PlayerPresent()
        {
            var p = Player.m_localPlayer;
            ModLog.Verbose("InGameTests: World_PlayerPresent player " + (p != null ? p.GetPlayerName() : "null"));
            return p != null ? "PASS: " + p.GetPlayerName() + " at " + p.transform.position.ToString("F1") : "FAIL: no local player 5 s after the world loaded";
        }
    }
}
