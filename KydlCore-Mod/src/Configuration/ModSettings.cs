using BepInEx.Configuration;
using KydlCore.Common;
using UnityEngine;

namespace KydlCore.Configuration;

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
    // [General]   NO EnableAll (MIGRATION_DECISIONS B12, user 2026-11-03): KydlCore and everything it does by default can never be switched off
    /// Opens / closes the KY hub (D-015, PLAN T-030). F6 is not bound by the game: a scan of the game assemblies for KeyCode.F6 (287) finds it
    /// only in ZInput's KeyCode -> InputSystem.Key conversion table, never as a button or a Input.GetKey call (2026-10-30). Read where it is
    /// used (PROJECT_RULES 17), never cached.
    public static ConfigEntry<KeyCode> HubKey;
    // [DebugLog]
    public static ConfigEntry<bool> DebugLog;


    // --- dev-only fixed values (Tunables): not in the config file, KydlTools changes them for testing -----------------------------------
    public static readonly Tunable<float> ExampleSpeed = new Tunable<float>("Example", "ExampleSpeed", 1f, 0f, 10f,
        "Example dev-only value (0-10, with a range as PROJECT_RULES 11 asks): replace or remove. Read it with ModSettings.ExampleSpeed.Value.");

    /// Expected outcome: every player entry is bound (returns that check).
    public static bool Bind(ConfigFile c)
    {
        var savedLines = ConfigTools.Capture(c);     // before the first Bind: every Bind saves the file with what BepInEx parsed

        HubKey = c.Bind("General", "HubKey", KeyCode.F6,
            "Opens / closes the KY hub, the one menu of every KydlDev mod. Pick any key from the list; None = the hub has no key. Takes effect at once.");
        DebugLog = c.Bind("DebugLog", "Enable", false,
            "Writes detailed lines about everything the mod does to the BepInEx log (for bug reports). Leave off while playing normally.");

        ConfigTools.ApplyCommaFix(c, savedLines);    // after every Bind: "3,5" typed by hand means 3.5, not 35
        ConfigTools.DropRemoved(c);

        HubKey.SettingChanged += (s, e) => ModLog.Info("Config: HubKey = " + HubKey.Value + " (read on every key check, so the change applies at once)");
        ModLog.Verbose("Config: bound (hub key " + HubKey.Value + ", debug log " + DebugLog.Value + ")");
        return HubKey != null && DebugLog != null;
    }
}
