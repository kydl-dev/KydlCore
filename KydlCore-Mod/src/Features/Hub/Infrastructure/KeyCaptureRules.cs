using System.Collections.Generic;
using UnityEngine;

namespace KydlCore.Features.Hub.Infrastructure;

/// <summary>What a press-to-rebind capture does with one frame of key presses.</summary>
internal enum CaptureStep
{
    /// <summary>Nothing usable was pressed: keep waiting.</summary>
    Waiting,
    /// <summary>Escape: leave the setting as it was.</summary>
    Cancelled,
    /// <summary>A key was chosen (and, for a shortcut, the modifiers held with it).</summary>
    Done
}

/// <summary>
/// Pure rules of the press-to-rebind button of the auto Settings page (user 2026-10-30, D-075): which key a frame of presses means, which of the
/// held Shift / Ctrl / Alt keys join a shortcut, and how a key or shortcut is written on the button. Only Unity's plain KeyCode enum, no engine
/// calls, no logging (like ConfigControlMap): the page and <c>KeyCapture</c> read the keyboard and write the entry.
/// </summary>
internal static class KeyCaptureRules
{
    /// <summary>Shift, Control and Alt (left or right) are the modifiers of a shortcut.</summary>
    public static bool IsModifier(KeyCode key)
    {
        return key == KeyCode.LeftShift || key == KeyCode.RightShift || key == KeyCode.LeftControl || key == KeyCode.RightControl ||
               key == KeyCode.LeftAlt || key == KeyCode.RightAlt;
    }

    /// <summary>
    /// Decides one frame. `pressed` = the keys that went down in this frame, `held` = the modifier keys down right now. Escape cancels. The left
    /// mouse button is the click on the button itself and never counts. For a shortcut (`withModifiers`) a modifier alone is not a choice, the
    /// capture waits for the key it modifies and takes the held modifiers with it; for a single key every key counts, a modifier too.
    /// </summary>
    public static CaptureStep Decide(IList<KeyCode> pressed, IList<KeyCode> held, bool withModifiers, out KeyCode main, out List<KeyCode> modifiers)
    {
        main = KeyCode.None;
        modifiers = new List<KeyCode>();
        if (pressed == null) return CaptureStep.Waiting;
        if (pressed.Contains(KeyCode.Escape)) return CaptureStep.Cancelled;
        foreach (KeyCode key in pressed)
        {
            if (key == KeyCode.None || key == KeyCode.Mouse0) continue;
            if (withModifiers && IsModifier(key)) continue;
            main = key;
            if (withModifiers && held != null)
                foreach (KeyCode m in held)
                    if (m != key && IsModifier(m) && !modifiers.Contains(m)) modifiers.Add(m);
            return CaptureStep.Done;
        }
        return CaptureStep.Waiting;
    }

    /// <summary>A key as the button shows it: the digit row as 0..9, nothing as "(none)", every other key by its name.</summary>
    public static string KeyName(KeyCode key)
    {
        if (key == KeyCode.None) return "(none)";
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)key - (int)KeyCode.Alpha0).ToString();
        return key.ToString();
    }

    /// <summary>A shortcut as the button shows it: the modifiers, then the key, joined with " + " ("LeftControl + K").</summary>
    public static string ShortcutText(KeyCode main, IList<KeyCode> modifiers)
    {
        if (main == KeyCode.None) return "(none)";
        var parts = new List<string>();
        if (modifiers != null) foreach (KeyCode m in modifiers) parts.Add(KeyName(m));
        parts.Add(KeyName(main));
        return string.Join(" + ", parts.ToArray());
    }
}
