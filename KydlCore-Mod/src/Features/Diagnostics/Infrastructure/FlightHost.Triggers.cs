using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using KydlCore.Common;
using KydlCore.Configuration;
using KydlCore.Features.Diagnostics.Domain;

namespace KydlCore.Features.Diagnostics.Infrastructure;

/// <summary>The flight recorder's triggers (any thread: they only queue) and its dumps (main thread). See FlightHost.cs.</summary>
internal static partial class FlightHost
{    // ---- triggers (any thread: queue only) ----------------------------------------------------------------------------------------

    /// <summary>DrakesTestLab finished a test: a note, and a dump when it failed.</summary>
    internal static void LabResult(string test, bool failed, string why)
    {
        lock (Gate)
        {
            if (_book == null) return;
            double now = Now;
            string took = _testStart >= 0 ? " after " + (now - _testStart).ToString("0", CultureInfo.InvariantCulture) + " s" : "";
            _book.Note(new FlightNote(now, "TestLab", "test " + test + ": " + (failed ? "FAILED" + took + " - " + TriggerRules.Short(why, 400) : "passed" + took)));
            if (failed) PendingDumps.Enqueue(new KeyValuePair<string, string>("testlab", "TestLab failed: " + test + " - " + TriggerRules.Short(why, 200)));
        }
    }

    /// <summary>DrakesTestLab reset its state after a test: the next test starts now.</summary>
    internal static void LabReset()
    {
        lock (Gate)
        {
            if (_book == null) return;
            _testStart = Now;
            _book.Note(new FlightNote(_testStart, "TestLab", "state reset - the next test starts"));
        }
    }

    /// <summary>Is this log source one of the KydlDev mods (a mod that uses ModLog)?</summary>
    internal static bool IsKydlMod(string source) => source != null && Array.IndexOf(ModContexts.Names(), source) >= 0;

    /// <summary>A KydlDev mod wrote an error: a note and a dump (at most one per mod per minute).</summary>
    internal static void ModError(string source, string text)
    {
        lock (Gate)
        {
            if (_book == null) return;
            _book.Note(new FlightNote(Now, source, "ERROR " + TriggerRules.Short(text, 400)));
            PendingDumps.Enqueue(new KeyValuePair<string, string>("error:" + source, "error in " + source + ": " + TriggerRules.Short(text, 200)));
        }
    }

    static void OnCheckOff(ModContext c, string text)
    {
        lock (Gate)
        {
            if (_book == null) return;
            _book.Note(new FlightNote(Now, c.Name, "CHECK OFF: " + text));
            PendingDumps.Enqueue(new KeyValuePair<string, string>("check-off:" + c.Name, "CHECK OFF in " + c.Name + ": " + TriggerRules.Short(text, 200)));
        }
    }

    static void OnNote(ModContext c, string text)
    {
        lock (Gate)
        {
            if (_book != null) _book.Note(new FlightNote(Now, c.Name, text));
        }
    }

    // ---- dumps (main thread) ------------------------------------------------------------------------------------------------------

    /// <summary>A mod asked for a dump (FlightRecorder.Dump, e.g. its watchdog fired). Off the main thread it is queued for the next frame.</summary>
    /// <returns>The file's path, or null (queued, refused by the gate, or the recorder is off).</returns>
    internal static string DumpNow(string mod, string reason)
    {
        if (!_started)
        {
            return null;
        }

        string full = mod + ": " + (reason ?? "(no reason given)");
        if (Thread.CurrentThread.ManagedThreadId != _mainThread)
        {
            lock (Gate) PendingDumps.Enqueue(new KeyValuePair<string, string>("manual:" + mod, full));
            return null;
        }

        double now = Now;
        Sample(now);              // the newest moment is in the dump
        _next = now + Interval();
        Note(mod, "dump asked: " + reason);
        return WriteDump("manual:" + mod, full, now);
    }

    static void DrainDumps(double now)
    {
        while (true)
        {
            KeyValuePair<string, string> d;
            lock (Gate)
            {
                if (PendingDumps.Count == 0) return;
                d = PendingDumps.Dequeue();
            }

            WriteDump(d.Key, d.Value, now);
        }
    }

    static string WriteDump(string kind, string reason, double now)
    {
        string text;
        lock (Gate)
        {
            if (!_gate.Allow(kind, now, Gap(kind), ModSettings.FlightMaxDumps.Value))
            {
                ModLog.Verbose("FlightRecorder: no dump for '" + reason + "' (" + kind + " had one less than " + Gap(kind) + " s ago, or the limit of "
                               + ModSettings.FlightMaxDumps.Value + " per game start is reached; " + _gate.Refused + " refused so far)");
                return null;
            }

            text = _book.Format(reason, now, Window(), DateTime.Now, MaxTracksInDump);
        }

        string path = FlightFiles.Write(reason, text);
        if (path != null)
        {
            LastDumpPath = path;
            ModLog.Info("FlightRecorder: dump written " + path + " (" + reason + ")");
        }

        return path;
    }

    // a failed test always gets its dump; repeated CHECK OFF / errors of one mod at most one per minute
    static double Gap(string kind) =>
        kind.StartsWith("testlab", StringComparison.Ordinal) ? 0 : kind.StartsWith("manual", StringComparison.Ordinal) ? 5 : 60;

    static void WarnSoon(string text)
    {
        if (Now < _nextWarn) return;
        _nextWarn = Now + 10;
        ModLog.Warn(text);
    }
}
