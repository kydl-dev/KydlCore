using System;
using UnityEngine.UIElements;

namespace KydlCore
{
    /// <summary>
    /// Windows OUTSIDE the hub (DESIGN 3, D-019, PLAN T-038): a window tied to something in the world, like ChestDecor's chest editor, opened by the
    /// interaction (never by a mod hotkey of its own). Everything else is a tab in <see cref="KyHub"/>. Core owns what must exist once: the UI root,
    /// the window order, the input lock (held while ANY Core window is open) and Escape (closes only the front window). The mod builds the window
    /// content with the shared kit (<c>Kydl.Ui.UiKit.Window</c> ...), passes it to <see cref="Open"/> and cleans up in `onClosed`.
    /// </summary>
    public static class KyUi
    {
        /// <summary>The one UI root of the game's KydlDev UI (null while the UI is not available yet).</summary>
        public static VisualElement Root { get { return Kydl.Ui.UiKit.Root; } }

        /// <summary>True while any Core window (the hub included) is open: the game takes no input then.</summary>
        public static bool AnyOpen { get { return KyWindows.Stack.AnyOpen; } }

        /// <summary>
        /// Puts `window` on the UI root, in front, and holds the input lock. A window with the same GUID and id that is open already is closed first
        /// (its `onClosed` runs). `onClosed` runs once when the window closes by any way (Escape, <see cref="Close"/>, <see cref="KyWindow.Close"/>);
        /// the window element is already removed then. Returns null (and logs why) for a blank GUID or id, a null window or when the UI is not available yet.
        /// </summary>
        public static KyWindow Open(string modGuid, string id, VisualElement window, Action onClosed = null)
        {
            return KyWindows.Add(modGuid, id, window, onClosed);
        }

        /// <summary>Closes a window of a mod (its `onClosed` runs). False when it was not open.</summary>
        public static bool Close(string modGuid, string id) { return KyWindows.Remove(KydlCore.Ui.KyWindowId.Make(modGuid, id), "Close"); }

        /// <summary>Is the window of this mod and id open?</summary>
        public static bool IsOpen(string modGuid, string id)
        {
            string full = KydlCore.Ui.KyWindowId.Make(modGuid, id);
            return full != null && KyWindows.Stack.IsOpen(full);
        }
    }

    /// <summary>A window opened with <see cref="KyUi.Open"/>.</summary>
    public sealed class KyWindow
    {
        internal KyWindow(string modGuid, string id, string fullId, VisualElement element, Action onClosed)
        {
            ModGuid = modGuid; Id = id; FullId = fullId; Element = element; OnClosed = onClosed;
        }

        /// <summary>The plugin GUID of the mod that owns the window.</summary>
        public string ModGuid { get; private set; }

        /// <summary>The mod's own id of the window.</summary>
        public string Id { get; private set; }

        internal string FullId { get; private set; }
        internal VisualElement Element { get; private set; }
        internal Action OnClosed { get; private set; }

        /// <summary>Closes the window (its `onClosed` runs). Safe to call twice.</summary>
        public void Close() { KyWindows.Remove(FullId, "KyWindow.Close"); }

        /// <summary>Brings the window in front of the others.</summary>
        public void BringToFront() { KyWindows.Front(FullId); }
    }
}
