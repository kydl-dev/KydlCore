using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KydlCore.Common;

/// <summary>
/// SHARED (user: "don't move the character when interacting with the UI menu"). While one of the mod's menus is open the game behaves as if
/// its own text input box were open: the game asks TextInput.IsVisible() before walking / looking (PlayerController.TakeInput, Player.TakeInput),
/// before locking the mouse (GameCamera.UpdateMouseCapture), before Escape opens the main menu (Menu), before the map / chat keys
/// (Minimap, Chat): one postfix covers all of them. The mouse wheel is also kept from zooming.
/// Patches are made by hand, only when the game method exists, so a renamed method costs the lock, not the mod.
/// Logs through KydlCore.Ui.UiLog (the mod points it at its ModLog), so KydlTools can use this file without the shared ModLog.
/// </summary>
public static class UiInputLock
{
    static readonly HashSet<string> Holders = new HashSet<string>();
    static Harmony _harmony;
    static int _patched;

    /// <summary>Number of open menus that hold the lock (0 = the game has its input).</summary>
    public static int Count { get { return Holders.Count; } }

    /// <summary>Installs the game patches; call it once, from Plugin.Awake. Returns whether at least TextInput.IsVisible is patched.</summary>
    public static bool Install(string modGuid)
    {
        if (_harmony != null) return _patched > 0;
        try
        {
            _harmony = new Harmony(modGuid + ".uilock");
            var vis = AccessTools.Method(typeof(TextInput), "IsVisible");
            if (vis != null) { _harmony.Patch(vis, postfix: new HarmonyMethod(typeof(UiInputLock), nameof(VisiblePostfix))); _patched++; }
            else KydlCore.Ui.UiLog.Warn("UiInputLock: TextInput.IsVisible not found - the player can still move while a menu is open");
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(ZInput)))
            {
                if (m.Name != "GetMouseScrollWheel" || m.ReturnType != typeof(float) || m.IsGenericMethod) continue;
                _harmony.Patch(m, postfix: new HarmonyMethod(typeof(UiInputLock), nameof(ScrollPostfix)));
                _patched++;
            }
            KydlCore.Ui.UiLog.Verbose("UiInputLock: " + _patched + " game method(s) patched (TextInput.IsVisible + ZInput.GetMouseScrollWheel)");
        }
        catch (HarmonyException e) { KydlCore.Ui.UiLog.Warn("UiInputLock: patching failed: " + e.Message); }
        catch (System.Exception e) { KydlCore.Ui.UiLog.Warn("UiInputLock: install failed: " + e.Message); }
        return _patched > 0;
    }

    /// <summary>A menu opens: the game stops taking input, the mouse is freed. Returns whether `who` is a holder.</summary>
    public static bool Hold(string who)
    {
        Holders.Add(who);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        KydlCore.Ui.UiLog.Verbose("UiInputLock: held by '" + who + "' (" + Holders.Count + " holder(s))");
        return Holders.Contains(who);
    }

    /// <summary>A menu closes; the game gets its input back when the last holder lets go. Returns whether `who` is no holder any more.</summary>
    public static bool Release(string who)
    {
        int before = Holders.Count;
        Holders.Remove(who);
        if (before != Holders.Count) KydlCore.Ui.UiLog.Verbose("UiInputLock: released by '" + who + "' (" + Holders.Count + " holder(s) left)");
        return !Holders.Contains(who);
    }

    static void VisiblePostfix(ref bool __result) { if (Holders.Count > 0) __result = true; }
    static void ScrollPostfix(ref float __result) { if (Holders.Count > 0) __result = 0f; }
}
