using System;
using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace KydlCore.Ui
{
    /// <summary>
    /// The GAME side of the press-to-rebind button (D-075): while a capture is on, it reads the keyboard every frame (Plugin.Update, before the hub key
    /// and Escape are looked at) and hands what was pressed to <see cref="KeyCaptureRules"/>. While it runs, and in the frame it ends, the hub key and
    /// Escape do nothing else (<see cref="Blocks"/>), so pressing the hub's own key to rebind it does not close the hub and Escape only cancels.
    /// </summary>
    internal static class KeyCapture
    {
        static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        static readonly KeyCode[] Modifiers = { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };

        static Action<KeyCode, List<KeyCode>> _done;
        static Action _cancelled;
        static bool _withModifiers;
        static int _startFrame = -1, _endFrame = -1;

        /// <summary>A capture is waiting for a key.</summary>
        internal static bool Active { get { return _done != null; } }

        /// <summary>True while a capture runs and in the frame it ended: the hub key and Escape must not act on that press.</summary>
        internal static bool Blocks { get { return Active || _endFrame == Time.frameCount; } }

        /// <summary>
        /// Starts waiting for a key (an earlier capture is cancelled). `withModifiers` = a shortcut: held Shift / Ctrl / Alt join the key.
        /// `done` gets the key and the modifiers, `cancelled` runs when Escape or <see cref="Cancel"/> ends it.
        /// </summary>
        internal static void Begin(bool withModifiers, Action<KeyCode, List<KeyCode>> done, Action cancelled)
        {
            Cancel("another capture started");
            _withModifiers = withModifiers;
            _done = done;
            _cancelled = cancelled;
            _startFrame = Time.frameCount;
            ModLog.Verbose("KeyCapture: waiting for a " + (withModifiers ? "shortcut" : "key"));
        }

        /// <summary>Ends a running capture without a result (the page closed or was redrawn). Expected outcome: not active afterwards.</summary>
        internal static bool Cancel(string why)
        {
            if (!Active) return true;
            var cancelled = _cancelled;
            Clear();
            ModLog.Verbose("KeyCapture: cancelled (" + why + ")");
            if (cancelled != null) cancelled();
            return !Active;
        }

        /// <summary>Every frame (Plugin.Update, first). Expected outcome: true when this frame ended the capture.</summary>
        internal static bool Tick()
        {
            if (!Active || Time.frameCount == _startFrame) return false;     // the click that started it is not the key
            var pressed = new List<KeyCode>();
            foreach (KeyCode key in AllKeys)
                if (key != KeyCode.None && Input.GetKeyDown(key) && !pressed.Contains(key)) pressed.Add(key);
            if (pressed.Count == 0) return false;
            var held = new List<KeyCode>();
            foreach (KeyCode m in Modifiers) if (Input.GetKey(m)) held.Add(m);

            KeyCode main;
            List<KeyCode> mods;
            CaptureStep step = KeyCaptureRules.Decide(pressed, held, _withModifiers, out main, out mods);
            if (step == CaptureStep.Waiting) return false;
            var done = _done;
            var cancelled = _cancelled;
            Clear();
            if (step == CaptureStep.Cancelled)
            {
                ModLog.Verbose("KeyCapture: Escape - the setting stays");
                if (cancelled != null) cancelled();
            }
            else
            {
                ModLog.Verbose("KeyCapture: " + KeyCaptureRules.ShortcutText(main, mods));
                if (done != null) done(main, mods);
            }
            return true;
        }

        static void Clear()
        {
            _done = null;
            _cancelled = null;
            _endFrame = Time.frameCount;
        }
    }
}
