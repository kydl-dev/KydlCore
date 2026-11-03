using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace KydlCore.Common;

/// <summary>
/// SHARED, lives in KydlCore.dll (KydlCore-Mod\src\Shared\Common). The ONLY place a mod writes log output:
/// every other file calls ModLog.Info / Warn / Error / Verbose / VerboseWarn and never touches a BepInEx logger directly.
///
/// User standards (every mod, from the start):
///  * Debug messages everywhere, written only while the mod's "Debug logging" switch is on (Verbose / VerboseWarn). With it on, everything is tracked.
///  * Sanitized log: a line identical to one written less than 2.5 s ago (same level + text) is not written again. Errors are always written.
/// One class for all mods: the state (log source, debug switch, repeat filter) is kept per CALLING mod in a <see cref="ModContext"/>, so a line always carries the name of the mod that wrote it.
/// Every line carries the date and time; the first line of a game launch is "{date&amp;time} - NEW LOG SESSION STARTS HERE".
/// </summary>
public static class ModLog
{
    // ---- look of a log line: edit here and nowhere else ------------------------------------------------------------
    const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
    const string LineFormat = "{0} - {1}";                    // {0} = date & time, {1} = the message
    const string SessionText = "NEW LOG SESSION STARTS HERE";
    // ----------------------------------------------------------------------------------------------------------------

    /// Call once, first thing in Plugin.Awake. `debugOn` reads the mod's "Debug logging" config entry (it is bound later, so it is a function).
    /// Expected outcome: the session line is written once; returns whether the source is set.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Init(ManualLogSource source, string modName, Func<bool> debugOn)
    {
        var ctx = ModContexts.For(Assembly.GetCallingAssembly());
        ctx.Source = source;
        if (!string.IsNullOrEmpty(modName)) ctx.Name = modName;
        ctx.DebugOn = debugOn;
        StartSession(ctx);
        return ctx.Source == source;
    }

    static void StartSession(ModContext ctx)
    {
        if (ctx.SessionStarted) return;
        ctx.SessionStarted = true;
        Info(ctx, SessionText);
    }

    // Falls back to a source named after the mod when something logs before Init (so no line is ever lost or throws).
    static ManualLogSource Source(ModContext ctx)
    {
        if (ctx.Source == null) ctx.Source = Logger.CreateLogSource(ctx.Name);
        return ctx.Source;
    }

    static string Line(string message)
    {
        return string.Format(CultureInfo.InvariantCulture, LineFormat, DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture), message ?? "(null)");
    }

    // The public entry points resolve the calling mod with Assembly.GetCallingAssembly() (NoInlining keeps that the mod, not KydlCore). Code inside KydlCore
    // that works on behalf of a mod passes the mod's context to the overloads below instead.
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Info(string message) { Write(ModContexts.For(Assembly.GetCallingAssembly()), LogLevel.Info, message); }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Warn(string message) { Write(ModContexts.For(Assembly.GetCallingAssembly()), LogLevel.Warning, message); }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Error(string message) { Write(ModContexts.For(Assembly.GetCallingAssembly()), LogLevel.Error, message); }

    /// Info that is only written while the mod's debug switch is on.
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Verbose(string message) { var c = ModContexts.For(Assembly.GetCallingAssembly()); if (IsDebugOn(c)) Info(c, message); }
    /// Warning that is only written while the mod's debug switch is on.
    [MethodImpl(MethodImplOptions.NoInlining)] public static void VerboseWarn(string message) { var c = ModContexts.For(Assembly.GetCallingAssembly()); if (IsDebugOn(c)) Warn(c, message); }

    // On behalf of a mod (its context is known).
    internal static void Info(ModContext c, string message) { Write(c, LogLevel.Info, message); }
    internal static void Warn(ModContext c, string message) { Write(c, LogLevel.Warning, message); }
    internal static void Error(ModContext c, string message) { Write(c, LogLevel.Error, message); }
    internal static void Verbose(ModContext c, string message) { if (IsDebugOn(c)) Info(c, message); }
    internal static void VerboseWarn(ModContext c, string message) { if (IsDebugOn(c)) Warn(c, message); }

    // ---- expected-value checks (user, 2026-10-24: "something not aligned with the expected result should be a warning at the very least")
    // A diagnostic number that the code can predict is logged WITH its prediction. Inside the tolerance it is a debug line (only with the
    // debug switch on); outside it is a WARNING that is always written, so it is seen without searching. Prefix "CHECK" lets GameProbe count them.

    /// actual should equal expected within +- tolerance. Expected outcome: returns whether it was inside (a warning was written when not).
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Check(string what, double actual, double expected, double tolerance, string unit) { return Check(ModContexts.For(Assembly.GetCallingAssembly()), what, actual, expected, tolerance, unit); }

    internal static bool Check(ModContext c, string what, double actual, double expected, double tolerance, string unit)
    {
        double off = actual - expected;
        bool inside = !double.IsNaN(actual) && Math.Abs(off) <= Math.Abs(tolerance);
        string text = what + " = " + Num(actual) + Unit(unit) + " (expected " + Num(expected) + " +- " + Num(Math.Abs(tolerance)) + Unit(unit) + ")";
        if (inside) CheckOk(c, what, text);
        else Warn(c, "CHECK OFF: " + text + ", off by " + Num(off) + Unit(unit));
        return inside;
    }

    /// actual should lie in [min, max]. Expected outcome: returns whether it was inside (a warning was written when not).
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool CheckRange(string what, double actual, double min, double max, string unit) { return CheckRange(ModContexts.For(Assembly.GetCallingAssembly()), what, actual, min, max, unit); }

    internal static bool CheckRange(ModContext c, string what, double actual, double min, double max, string unit)
    {
        bool inside = !double.IsNaN(actual) && actual >= min && actual <= max;
        string text = what + " = " + Num(actual) + Unit(unit) + " (expected " + Num(min) + " .. " + Num(max) + Unit(unit) + ")";
        if (inside) CheckOk(c, what, text);
        else Warn(c, "CHECK OFF: " + text);
        return inside;
    }

    // A passing check that repeats (once per tick, once per zone ...) would fill the log: per check name one "ok" line every CheckOkSeconds,
    // with how many were left out. A check that is OFF is always written (Warn above).
    const double CheckOkSeconds = 30.0;
    static void CheckOk(ModContext c, string what, string text)
    {
        var OkSeen = c.OkSeen;
        if (!IsDebugOn(c)) return;
        long now = Stopwatch.GetTimestamp();
        KeyValuePair<long, int> e;
        if (OkSeen.TryGetValue(what, out e) && (now - e.Key) / (double)Stopwatch.Frequency < CheckOkSeconds) { OkSeen[what] = new KeyValuePair<long, int>(e.Key, e.Value + 1); return; }
        if (OkSeen.Count > 500) OkSeen.Clear();
        OkSeen[what] = new KeyValuePair<long, int>(now, 0);
        Info(c, "CHECK ok: " + text + (e.Value > 0 ? " [" + e.Value + " more of this check ok in the last " + CheckOkSeconds.ToString("0") + " s not written]" : ""));
    }

    static string Num(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
    static string Unit(string unit) { return string.IsNullOrEmpty(unit) ? "" : " " + unit; }

    /// Is the calling mod's debug switch on? A failing reader counts as off (logging must never take the game down).
    public static bool DebugOn
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get { return IsDebugOn(ModContexts.For(Assembly.GetCallingAssembly())); }
    }

    internal static bool IsDebugOn(ModContext c)
    {
        try { return c.DebugOn != null && c.DebugOn(); }
        catch (NullReferenceException) { return false; }      // the config entry is not bound yet
    }

    // ---- repeat filter (user standard, 2026-10-20) --------------------------------------------------------------------
    // Measured with a Stopwatch (no Unity time), so it works before the game runs and in the unit tests.
    const double RepeatWindowSeconds = 2.5;
    const int MaxRemembered = 4000;
    static readonly Stopwatch Clock = Stopwatch.StartNew();

    /// Test support: forget every line seen so far (a fresh game launch). Expected outcome: the table is empty afterwards.
    internal static bool ResetRepeats(ModContext c) { lock (c.RepeatLock) { c.LastWritten.Clear(); return c.LastWritten.Count == 0; } }

    /// Expected outcome: true exactly when the same level + text was written within the window (then it is skipped).
    internal static bool IsRepeat(ModContext c, LogLevel level, string message)
    {
        var LastWritten = c.LastWritten;
        if (level == LogLevel.Error) return false;
        // Stryker disable once String : the separator only matters if a level number could run into the text; the levels used (4, 16) can't
        string key = (int)level + "|" + (message ?? "");
        double now = Clock.Elapsed.TotalSeconds, last;
        lock (c.RepeatLock)
        {
            // Stryker disable once Equality : < vs <= differ only when the Stopwatch gap is exactly 2.5 s
            bool repeat = LastWritten.TryGetValue(key, out last) && now - last < RepeatWindowSeconds;
            if (!repeat) LastWritten[key] = now;
            if (LastWritten.Count > MaxRemembered) DropOld(LastWritten, now);
            return repeat;
        }
    }

    static void DropOld(Dictionary<string, double> LastWritten, double now)
    {
        var old = new List<string>();
        // Stryker disable once Equality : >= vs > differ only when a line is exactly 2.5 s old
        foreach (var kv in LastWritten) if (now - kv.Value >= RepeatWindowSeconds) old.Add(kv.Key);
        foreach (var k in old) LastWritten.Remove(k);
    }

    // The single line that actually writes. A failure while logging is swallowed: logging must never take the game down.
    static void Write(ModContext c, LogLevel level, string message)
    {
        try
        {
            if (IsRepeat(c, level, message)) return;
            Source(c).Log(level, Line(message));
        }
        catch (ObjectDisposedException) { }          // the BepInEx log is closed while the game shuts down
        catch (Exception) { }                        // last resort: never throw out of the logger
    }
}