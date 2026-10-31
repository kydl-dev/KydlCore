using System;
using System.Collections.Generic;
using Kydl.Common;
using Kydl.Ui;
using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore
{
    /// <summary>
    /// The GAME side of the window order (PLAN T-038): the ONE <see cref="WindowStack"/> of the hub and every <see cref="KyUi"/> window, the input lock
    /// that goes with it (first window opens = lock held, last one closes = released), a click that brings a window to the front, and Escape that closes
    /// only the front one. The order logic itself is the pure, tested WindowStack; the ids are built by KyWindowId.
    /// </summary>
    internal static class KyWindows
    {
        const string LockName = "KydlCore.Windows";

        internal static readonly WindowStack Stack = new WindowStack();
        static readonly Dictionary<string, KyWindow> Windows = new Dictionary<string, KyWindow>();
        static bool _wired;

        /// <summary>The first window opening takes the one input lock, the last one closing gives it back. Called before any window opens.</summary>
        internal static void EnsureWired()
        {
            if (_wired) return;
            _wired = true;
            Stack.AnyOpenChanged += open =>
            {
                if (open) UiInputLock.Hold(LockName);
                else UiInputLock.Release(LockName);
                ModLog.Verbose("KyWindows: any window open " + open + ", input lock " + (open ? "held" : "released"));
            };
        }

        internal static KyWindow Add(string modGuid, string id, VisualElement window, Action onClosed)
        {
            string full = KyWindowId.Make(modGuid, id);
            if (full == null || window == null)
            {
                ModLog.Warn("KyUi.Open: refused (guid '" + modGuid + "', id '" + id + "', window " + (window == null ? "null" : "given") + ")");
                return null;
            }
            var root = UiKit.Root;
            if (root == null) { ModLog.Warn("KyUi.Open: the UI is not available yet, '" + full + "' not opened"); return null; }
            EnsureWired();
            if (Windows.ContainsKey(full)) Remove(full, "replaced by a new window with the same id");

            var w = new KyWindow(modGuid.Trim(), id.Trim(), full, window, onClosed);
            window.RegisterCallback<PointerDownEvent>(e => Front(full), TrickleDown.TrickleDown);
            root.Add(window);
            Windows[full] = w;
            Stack.Open(full);
            ModLog.Verbose("KyWindows: opened '" + full + "' (" + Stack.Count + " window(s) open)");
            return w;
        }

        /// <summary>Closes a mod window: element off the root, out of the stack (the lock goes with the last one), then the mod's `onClosed`.</summary>
        internal static bool Remove(string full, string why)
        {
            KyWindow w;
            if (full == null || !Windows.TryGetValue(full, out w)) return false;
            Windows.Remove(full);
            try { w.Element.RemoveFromHierarchy(); }
            catch (Exception e) { ModLog.Warn("KyWindows: removing '" + full + "' from the UI failed: " + e.Message); }
            Stack.Close(full);
            ModLog.Verbose("KyWindows: closed '" + full + "' (" + why + "), " + Stack.Count + " window(s) left");
            try { if (w.OnClosed != null) w.OnClosed(); }
            catch (Exception e) { ModLog.Error("KyWindows: onClosed of '" + full + "' failed: " + e); }
            return true;
        }

        /// <summary>A click on a window (or KyWindow.BringToFront): in front of the others, in the stack and on screen.</summary>
        internal static bool Front(string full)
        {
            KyWindow w;
            if (full == null || !Windows.TryGetValue(full, out w)) return false;
            if (Stack.Top != full) { Stack.BringToFront(full); w.Element.BringToFront(); ModLog.Verbose("KyWindows: '" + full + "' brought to the front"); }
            return true;
        }

        /// <summary>Every frame (Plugin.Update): Escape closes ONLY the front window (the hub or a mod's). Returns whether a window was closed.</summary>
        internal static bool Tick()
        {
            if (KeyCapture.Blocks) return false;           // Escape that cancels a key capture does not also close the window
            if (!Stack.AnyOpen || !ZInput.GetKeyDown(KeyCode.Escape, true)) return false;
            string top = Stack.Top;
            if (top == KyWindowId.Hub) HubHost.Close("Escape");
            else Remove(top, "Escape");
            return true;
        }

        /// <summary>Plugin shutdown: close every mod window (their `onClosed` run) so no chest stays locked and no input stays blocked.</summary>
        internal static int CloseAll(string why)
        {
            var ids = new List<string>(Windows.Keys);
            foreach (string id in ids) Remove(id, why);
            return ids.Count;
        }
    }
}
