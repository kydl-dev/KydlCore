using BepInEx.Configuration;
using Kydl.Common;
using UnityEngine;

namespace KydlCore
{
    /// <summary>
    /// All settings in one place (user rule, every mod):
    ///  * PLAYER CONFIG (BepInEx entries below, bound once from Plugin.Awake): only what a player should change. Clear descriptions, ranges,
    ///    drop-downs (enums). Descriptions never mention older versions, old names or old values.
    ///  * DEV-ONLY values (Tunable fields below): fixed values chosen by the user, NOT in the player's file. The user's KydlTools mod (F7) finds
    ///    every static Tunable here by itself and lets the user change it in game - nothing to register.
    /// When the user settles a value in game, it becomes the new fixed value / default here.
    /// </summary>
    internal static class ModSettings
    {
        // [General]
        public static ConfigEntry<bool> EnableAll;
        public static ConfigEntry<KeyCode> MenuKey;
        // [DebugLog]
        public static ConfigEntry<bool> DebugLog;

        /// Is the mod switched on ([General] EnableAll)? Off = inert: no effects, no menus; nothing built is removed.
        public static bool On { get { return EnableAll == null || EnableAll.Value; } }

        // --- dev-only fixed values (Tunables): not in the config file, KydlTools changes them for testing -----------------------------------
        public static readonly Tunable<float> ExampleSpeed = new Tunable<float>("Example", "ExampleSpeed", 1f, 0f, 10f,
            "Example dev-only value (0-10, with a range as PROJECT_RULES 11 asks): replace or remove. Read it with ModSettings.ExampleSpeed.Value.");

        /// Expected outcome: every player entry is bound (returns that check).
        public static bool Bind(ConfigFile c)
        {
            var savedLines = ConfigTools.Capture(c);     // before the first Bind: every Bind saves the file with what BepInEx parsed

            EnableAll = c.Bind("General", "EnableAll", true,
                "Switches the whole mod on / off while playing. Off: no effects and no menus from this mod; things already built stay in the world.");
            MenuKey = c.Bind("General", "MenuKey", KeyCode.F6,
                "Opens / closes the KydlCore menu. None = no menu.");
            DebugLog = c.Bind("DebugLog", "Enable", false,
                "Writes detailed lines about everything the mod does to the BepInEx log (for bug reports). Leave off while playing normally.");

            ConfigTools.ApplyCommaFix(c, savedLines);    // after every Bind: "3,5" typed by hand means 3.5, not 35
            ConfigTools.DropRemoved(c);

            EnableAll.SettingChanged += (s, e) => ModLog.Info("Config: EnableAll = " + EnableAll.Value + (EnableAll.Value ? ", the mod is on" : ", the mod is inert (no effects or menus)"));
            ModLog.Verbose("Config: bound (EnableAll " + EnableAll.Value + ", menu key " + MenuKey.Value + ", debug log " + DebugLog.Value + ")");
            return EnableAll != null && MenuKey != null && DebugLog != null;
        }
    }
}
