using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace KydlCore.Common;

/// <summary>
/// Diagnostics from the start (user, 2026-11-06, after a night of runs where the cause only showed in lines added afterwards): three helpers
/// that make the right log line the easy one, and the hooks KydlCore's flight recorder listens to.
///  * <see cref="State"/>: a state change with its reason ("STATE guard: idle -> fighting (Greyling attacks the owner)").
///  * <see cref="Decision"/>: a choice with its inputs and its outcome ("DECISION next target -> Beech1 (ordered source, 12 m)").
///  * <see cref="Every"/>: a status line for code that runs every frame, written at most once per key and interval.
/// All three are debug lines in the log (written only while the mod's debug switch is on) AND always go to the flight recorder's in-memory
/// timeline, so a dump after a failure shows them even when the debug log was off.
/// </summary>
public static partial class ModLog
{
    /// <summary>Raised for every CHECK OFF warning (mod context, the check's text). The flight recorder writes a dump.</summary>
    internal static event Action<ModContext, string> CheckOffWritten;

    /// <summary>Raised for every <see cref="State"/>, <see cref="Decision"/> and written <see cref="Every"/> line (mod context, text).</summary>
    internal static event Action<ModContext, string> NoteWritten;

    /// <summary>
    /// A state change: what changed, from which state to which, and why. Written as "STATE what: from -> to (reason)".
    /// </summary>
    /// <returns>Whether the state really changed (from and to differ).</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool State(string what, string from, string to, string reason)
    {
        var c = ModContexts.For(Assembly.GetCallingAssembly());
        string text = "STATE " + (what ?? "?") + ": " + (from ?? "-") + " -> " + (to ?? "-") + (string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")");
        Verbose(c, text);
        RaiseNote(c, text);
        return !string.Equals(from, to, StringComparison.Ordinal);
    }

    /// <summary>
    /// A decision: what was decided, the outcome, and the inputs that decided it (with units). Written as "DECISION what -> outcome (inputs)".
    /// </summary>
    /// <returns>The outcome, so a method can end with <c>return ModLog.Decision("next target", picked, "...")</c>.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string Decision(string what, string outcome, string inputs)
    {
        var c = ModContexts.For(Assembly.GetCallingAssembly());
        string text = "DECISION " + (what ?? "?") + " -> " + (outcome ?? "(none)") + (string.IsNullOrEmpty(inputs) ? "" : " (" + inputs + ")");
        Verbose(c, text);
        RaiseNote(c, text);
        return outcome;
    }

    /// <summary>
    /// A status line for per-frame code: written at most once every <paramref name="seconds"/> per <paramref name="key"/> (the first call
    /// always). When the message is expensive to build, check <see cref="IsDue"/> first: <c>if (ModLog.IsDue("chase", 5)) ModLog.Every("chase", 5, Describe());</c>.
    /// </summary>
    /// <returns>Whether the line was due (and was handed to the log and the flight recorder).</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Every(string key, double seconds, string message)
    {
        var c = ModContexts.For(Assembly.GetCallingAssembly());
        bool due = TakeDue(c, key, seconds);
        if (due)
        {
            string text = "STATUS " + (key ?? "?") + ": " + (message ?? "");
            Verbose(c, text);
            RaiseNote(c, text);
        }

        return due;
    }

    /// <summary>Would <see cref="Every"/> write this key now? (Does not use up the slot: call it to skip building an expensive message.)</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsDue(string key, double seconds)
    {
        var c = ModContexts.For(Assembly.GetCallingAssembly());
        lock (c.RepeatLock)
        {
            return !c.EveryLast.TryGetValue(key ?? "", out double last) || Clock.Elapsed.TotalSeconds - last >= Math.Max(0, seconds);
        }
    }

    static bool TakeDue(ModContext c, string key, double seconds)
    {
        string k = key ?? "";
        double now = Clock.Elapsed.TotalSeconds;
        lock (c.RepeatLock)
        {
            bool due = !c.EveryLast.TryGetValue(k, out double last) || now - last >= Math.Max(0, seconds);
            if (due)
            {
                if (c.EveryLast.Count > MaxRemembered) c.EveryLast.Clear();
                c.EveryLast[k] = now;
            }

            return due;
        }
    }

    // A listener that throws must never break the logger (logging must never take the game down).
    static void RaiseNote(ModContext c, string text)
    {
        var handler = NoteWritten;
        if (handler == null) return;
        try { handler(c, text); }
        catch (InvalidOperationException) { }      // the recorder is stopping (plugin unload)
        catch (Exception) { }                      // last resort
    }

    static void RaiseCheckOff(ModContext c, string text)
    {
        var handler = CheckOffWritten;
        if (handler == null) return;
        try { handler(c, text); }
        catch (InvalidOperationException) { }      // the recorder is stopping (plugin unload)
        catch (Exception) { }                      // last resort
    }
}
