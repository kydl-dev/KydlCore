using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using KydlCore.Common;
using KydlCore.Configuration;
using KydlCore.Features.Diagnostics.Domain;
using UnityEngine;

namespace KydlCore.Features.Diagnostics.Infrastructure;

/// <summary>
/// KydlCore's flight recorder (user, 2026-11-06): always on, in memory, near-zero cost. Every sample interval it records the environment
/// (frame rate, the game's simulation area, quality, the player) and every object a mod tracks (position, speed, health, AI target, network
/// owner, zone loaded, the mod's own facts - and for an object that is not loaded any more, where its saved data says it is). The timeline
/// also gets every ModLog.State / Decision / Every line, every CHECK OFF, DrakesTestLab's test results and errors of KydlDev mods.
/// Nothing is written until something goes wrong: a failed TestLab test, a CHECK OFF, an error of a KydlDev mod, or a mod's own
/// FlightRecorder.Dump (a watchdog). Then the last window is written to BepInEx\cache\KydlCore\flight\ (FlightFiles).
/// Threads: log lines and ModLog events arrive on any thread and only queue under <see cref="Gate"/>; sampling and writing run on the main
/// thread (Plugin.Update -> <see cref="Tick"/>).
/// </summary>
internal static partial class FlightHost
{
    sealed class Entry
    {
        internal Component Target;
        internal Character Character;
        internal MonsterAI Ai;
        internal ZNetView View;
        internal ZDOID Id;
        internal bool HasId;
        internal Func<string> Facts;
        internal string Mod;
        internal string Label;
        internal Vector3 LastPos;
        internal double LastTime = -1;
        internal bool Gone;
    }

    const int MaxTracked = 200;
    const int NotesKept = 800;
    const int MaxTracksInDump = 40;
    const double GoneKeepSeconds = 600;

    static readonly object Gate = new();
    static readonly Stopwatch Clock = new();
    static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);
    static readonly Queue<KeyValuePair<string, string>> PendingDumps = new();     // kind, reason
    static FlightBook _book;
    static DumpGate _gate;
    static FlightListener _listener;
    static int _mainThread;
    static volatile bool _started;
    static double _next;
    static double _lastEnv;
    static double _testStart = -1;
    static double _nextWarn;
    static int _frames;
    static int _capacity;
    static bool _fullWarned;

    /// <summary>The newest dump file, or null when none was written in this game start.</summary>
    internal static string LastDumpPath { get; private set; }

    /// <summary>How many objects are tracked now.</summary>
    internal static int TrackedCount { get { lock (Gate) { return Entries.Count; } } }

    static double Now => Clock.Elapsed.TotalSeconds;

    static double Interval() => Math.Max(0.25, Math.Min(10.0, ModSettings.FlightSampleSeconds.Value));

    static double Window() => Math.Max(30, Math.Min(600, ModSettings.FlightWindowSeconds.Value));

    static int Capacity() => Math.Max(2, Math.Min(2400, (int)Math.Ceiling(Window() / Interval()) + 1));

    // ---- start / stop (DiagnosticsFeature) ------------------------------------------------------------------------------------------

    /// <summary>Starts recording. Expected outcome: true when it is recording afterwards.</summary>
    internal static bool Start()
    {
        lock (Gate)
        {
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            Clock.Restart();
            _capacity = Capacity();
            _book = new FlightBook(_capacity, NotesKept, _capacity);
            _gate = new DumpGate();
            Entries.Clear();
            PendingDumps.Clear();
            _next = 0;
            _lastEnv = 0;
            _frames = 0;
        }

        _listener = new FlightListener();
        BepInEx.Logging.Logger.Listeners.Add(_listener);
        ModLog.CheckOffWritten += OnCheckOff;
        ModLog.NoteWritten += OnNote;
        int pruned = FlightFiles.Prune(ModSettings.FlightKeepDays.Value, ModSettings.FlightKeepFiles.Value);
        _started = true;
        ModLog.Verbose("FlightRecorder: started - sample every " + Interval().ToString("0.##", CultureInfo.InvariantCulture) + " s, window " + Window() + " s ("
                       + _capacity + " samples per object), at most " + ModSettings.FlightMaxDumps.Value + " dump(s) per game start, " + pruned
                       + " old dump(s) removed, folder " + FlightFiles.Dir());
        return _started;
    }

    /// <summary>Stops recording (plugin unload). Expected outcome: true when it is stopped afterwards.</summary>
    internal static bool Stop()
    {
        _started = false;
        ModLog.CheckOffWritten -= OnCheckOff;
        ModLog.NoteWritten -= OnNote;
        if (_listener != null)
        {
            BepInEx.Logging.Logger.Listeners.Remove(_listener);
            _listener = null;
        }

        ModLog.Verbose("FlightRecorder: stopped (" + (_gate != null ? _gate.Written : 0) + " dump(s) written this game start)");
        return !_started;
    }

    // ---- tracking (FlightRecorder, the public API) ---------------------------------------------------------------------------------

    /// <summary>Tracks an object for a mod (again: its target, label and facts are replaced, its history is kept).</summary>
    /// <returns>Whether it is tracked afterwards.</returns>
    internal static bool Track(ModContext mod, Component target, string label, Func<string> facts)
    {
        if (!_started || target == null)
        {
            return _started && target != null;
        }

        ZNetView view = target.GetComponentInParent<ZNetView>();
        bool hasId = view != null && view.IsValid();
        ZDOID id = hasId ? view.GetZDO().m_uid : default;
        string key = Key(target, hasId, id);
        lock (Gate)
        {
            if (!Entries.TryGetValue(key, out Entry e))
            {
                if (Entries.Count >= MaxTracked)
                {
                    WarnFullOnce(mod.Name, label);
                    return Entries.ContainsKey(key);
                }

                e = new Entry();
                Entries[key] = e;
            }

            e.Target = target;
            e.Character = target.GetComponentInParent<Character>();
            e.Ai = target.GetComponentInParent<MonsterAI>();
            e.View = view;
            e.Id = id;
            e.HasId = hasId;
            e.Facts = facts;
            e.Mod = mod.Name;
            e.Label = string.IsNullOrEmpty(label) ? target.name : label;
            e.Gone = false;
            e.LastTime = -1;
            _book.Start(key, e.Mod, e.Label, Now);
            _book.Note(new FlightNote(Now, e.Mod, "tracking " + e.Label + " (" + key + ")"));
            return _book.IsTracked(key);
        }
    }

    /// <summary>Stops tracking an object. Returns whether it was tracked.</summary>
    internal static bool Untrack(Component target)
    {
        if (target == null)
        {
            return target != null;
        }

        lock (Gate)
        {
            string key = null;
            foreach (var kv in Entries)
            {
                if (ReferenceEquals(kv.Value.Target, target))
                {
                    key = kv.Key;
                    break;
                }
            }

            bool removed = key != null && Entries.Remove(key);
            if (removed && _book != null)
            {
                _book.Stop(key);
            }

            return removed;
        }
    }

    /// <summary>A line on the timeline (any thread). Returns whether it was stored.</summary>
    internal static bool Note(string source, string text)
    {
        lock (Gate)
        {
            return _book != null && _book.Note(new FlightNote(Now, source, text)) > 0;
        }
    }

    static string Key(Component target, bool hasId, ZDOID id) =>
        hasId ? "zdo " + id : "obj " + target.GetInstanceID().ToString(CultureInfo.InvariantCulture);

    static void WarnFullOnce(string mod, string label)
    {
        if (_fullWarned)
        {
            return;
        }

        _fullWarned = true;
        ModLog.Warn("FlightRecorder: already " + MaxTracked + " objects tracked - " + mod + " / " + label + " and later ones are not recorded (untrack objects you no longer need)");
    }

    // ---- sampling (main thread) -----------------------------------------------------------------------------------------------------

    /// <summary>Every frame from Plugin.Update: counts the frame, samples when due, writes queued dumps.</summary>
    internal static void Tick()
    {
        if (!_started)
        {
            return;
        }

        _frames++;
        double now = Now;
        if (now >= _next)
        {
            _next = now + Interval();
            try
            {
                Sample(now);
            }
            catch (NullReferenceException e)
            {
                WarnSoon("FlightRecorder: a sample failed (an object went away while it was read): " + e.Message);
            }
            catch (Exception e)          // last resort: the recorder must never break KydlCore's Update
            {
                WarnSoon("FlightRecorder: a sample failed: " + e);
            }
        }

        DrainDumps(now);
    }

    static void Sample(double now)
    {
        List<KeyValuePair<string, Entry>> list;
        lock (Gate)
        {
            int cap = Capacity();
            if (cap != _capacity)
            {
                Rebuild(cap, now);
            }

            list = new List<KeyValuePair<string, Entry>>(Entries);
        }

        double fps = now > _lastEnv ? _frames / (now - _lastEnv) : 0;
        _frames = 0;
        _lastEnv = now;
        string env = FlightSampler.Environment(fps, list.Count);
        var samples = new List<KeyValuePair<string, FlightSample>>(list.Count);
        var gone = new List<KeyValuePair<string, string>>();
        foreach (var kv in list)
        {
            if (TrySample(kv.Value, now, out FlightSample s))
            {
                samples.Add(new KeyValuePair<string, FlightSample>(kv.Key, s));
            }
            else if (!kv.Value.Gone)
            {
                kv.Value.Gone = true;
                gone.Add(new KeyValuePair<string, string>(kv.Key, kv.Value.Mod));
            }
        }

        lock (Gate)
        {
            _book.Env(new FlightNote(now, "env", env));
            foreach (var s in samples)
            {
                _book.Add(s.Key, s.Value);
            }

            foreach (var g in gone)
            {
                _book.MarkGone(g.Key, now);
                _book.Note(new FlightNote(now, g.Value, "gone: " + g.Key + " (no object and no saved data left)"));
            }

            if (_book.Prune(now, GoneKeepSeconds) > 0)
            {
                var dropped = new List<string>();
                foreach (var kv in Entries)
                {
                    if (!_book.IsTracked(kv.Key)) dropped.Add(kv.Key);
                }

                foreach (string k in dropped) Entries.Remove(k);
            }
        }
    }

    // A changed window or interval (KydlTools) starts a fresh recording; the tracked objects stay tracked.
    static void Rebuild(int capacity, double now)
    {
        _capacity = capacity;
        _book = new FlightBook(capacity, NotesKept, capacity);
        foreach (var kv in Entries)
        {
            _book.Start(kv.Key, kv.Value.Mod, kv.Value.Label, now);
        }

        _book.Note(new FlightNote(now, "KydlCore", "recording restarted: window " + Window() + " s, sample every " + Interval().ToString("0.##", CultureInfo.InvariantCulture) + " s"));
    }

    static bool TrySample(Entry e, double now, out FlightSample sample)
    {
        if (e.Target != null)          // Unity's check: false once the object is destroyed or unloaded
        {
            Vector3 p = e.Target.transform.position;
            sample = new FlightSample(now, p.x, p.y, p.z, Speed(e, p, now), FlightSampler.Facts(e.Character, e.Ai, e.View, p) + FlightSampler.ModFacts(e.Facts));
            return sample.Time >= 0;
        }

        // not loaded any more: its saved data still says where it is (a far troll that froze in an unloaded zone, 2026-11-05)
        ZDO zdo = e.HasId && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(e.Id) : null;
        if (zdo != null)
        {
            Vector3 p = zdo.GetPosition();
            sample = new FlightSample(now, p.x, p.y, p.z, Speed(e, p, now), FlightSampler.UnloadedFacts(zdo, p));
            return sample.Time >= 0;
        }

        sample = default;
        return zdo != null;
    }

    static double Speed(Entry e, Vector3 p, double now)
    {
        double speed = e.LastTime >= 0 && now > e.LastTime ? Vector3.Distance(p, e.LastPos) / (now - e.LastTime) : 0;
        e.LastPos = p;
        e.LastTime = now;
        return speed;
    }
}
