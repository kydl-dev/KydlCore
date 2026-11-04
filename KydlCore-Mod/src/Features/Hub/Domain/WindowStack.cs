using System;
using System.Collections.Generic;

namespace KydlCore.Features.Hub.Domain;

/// <summary>
/// Which Core windows are open and in which order (DESIGN 4, PLAN T-021). Pure C#: no Unity, no game types, no logging, so it is unit tested.
/// The hub (id "hub") and every window opened through KyUi (T-032 / T-033) are entries here; the Unity side only draws them in this order.
///  * Open pushes a window on top; opening one that is already open only brings it to the front (an id is in the stack once).
///  * Clicking a window brings it to the front (BringToFront).
///  * Escape closes ONLY the top window (CloseTop); the ones below stay open.
///  * Close removes one window wherever it is.
///  * AnyOpenChanged fires on the two transitions "first window opened" (true) and "last window closed" (false), never in between: the
///    glue code holds / releases the one input lock with it, so the game takes no input while any Core window is open.
/// Ids are compared ordinally (case sensitive); null and "" are not valid ids. Main thread only (it is UI code).
/// An AnyOpenChanged handler runs AFTER the stack has changed; if it throws, the exception goes to the caller of Open / Close / CloseTop
/// and the stack keeps its new state.
/// </summary>
internal sealed class WindowStack
{
    readonly List<string> _ids = new List<string>();    // bottom ... top

    /// <summary>Fires with true when the first window opens and with false when the last one closes.</summary>
    public event Action<bool> AnyOpenChanged;

    /// <summary>Number of open windows.</summary>
    public int Count { get { return _ids.Count; } }

    /// <summary>Is at least one window open (= the input lock must be held)?</summary>
    public bool AnyOpen { get { return _ids.Count > 0; } }

    /// <summary>The id of the front window, or null when nothing is open.</summary>
    public string Top { get { return _ids.Count == 0 ? null : _ids[_ids.Count - 1]; } }

    /// <summary>Is a window with this id open? (null / "" are never open.)</summary>
    public bool IsOpen(string id) { return Valid(id) && _ids.Contains(id); }

    /// <summary>The open ids from the back to the front (a copy: changing it changes nothing).</summary>
    public string[] Ids() { return _ids.ToArray(); }

    /// <summary>
    /// Opens a window on top, or brings it to the front when it is already open. Returns true when the window is the front window afterwards,
    /// false (and nothing changes) for an invalid id.
    /// </summary>
    public bool Open(string id)
    {
        if (!Valid(id)) return false;
        bool wasOpen = AnyOpen;
        _ids.Remove(id);
        _ids.Add(id);
        Notify(wasOpen);
        return true;
    }

    /// <summary>A click on a window: brings an open window to the front. Returns false for a window that is not open.</summary>
    public bool BringToFront(string id)
    {
        if (!IsOpen(id)) return false;
        if (_ids[_ids.Count - 1] != id) { _ids.Remove(id); _ids.Add(id); }
        return true;
    }

    /// <summary>Closes one window wherever it is. Returns false for a window that is not open.</summary>
    public bool Close(string id)
    {
        if (!IsOpen(id)) return false;
        bool wasOpen = AnyOpen;
        _ids.Remove(id);
        Notify(wasOpen);
        return true;
    }

    /// <summary>Escape: closes only the front window and returns its id, or returns null when nothing is open.</summary>
    public string CloseTop()
    {
        if (_ids.Count == 0) return null;
        string top = _ids[_ids.Count - 1];
        Close(top);
        return top;
    }

    static bool Valid(string id) { return !string.IsNullOrEmpty(id); }

    void Notify(bool wasOpen)
    {
        bool now = AnyOpen;
        if (now != wasOpen && AnyOpenChanged != null) AnyOpenChanged(now);
    }
}
