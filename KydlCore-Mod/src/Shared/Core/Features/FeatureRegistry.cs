using System;
using System.Collections.Generic;

namespace KydlCore.Core;

/// <summary>
/// The mod's list of features (MIGRATION_DECISIONS A4). <see cref="Refresh"/> compares each feature's gate with its running state and
/// starts / stops the ones that differ; call it once after Bootstrap and on every config change (ConfigReloader / SettingChanged).
/// A feature that throws in Start / Stop is reported as Failed and stays in its old state; the other features are not affected.
/// Game-free: the trace sink is ModLog.Verbose in game, null in tests.
/// </summary>
public sealed class FeatureRegistry
{
    readonly List<IFeature> _features = new();
    readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    readonly Action<string> _trace;
    const bool BrokenGateIsOn = false;
    const bool ThrownMeansFinished = false;

    public FeatureRegistry(Action<string> trace = null) { _trace = trace; }

    public int Count => _features.Count;
    public IReadOnlyList<IFeature> All => _features;

    public bool IsRunning(string id) { return id != null && _running.Contains(id); }

    /// Adds a feature (not started yet). Expected outcome: Success with the feature count; Rejected for null or a duplicate id.
    public OperationResponse<int> Add(IFeature feature)
    {
        if (feature == null) return OperationResponse<int>.Rejected("null feature");
        if (string.IsNullOrEmpty(feature.Id)) return OperationResponse<int>.Rejected("feature without an id");
        foreach (var f in _features)
            if (string.Equals(f.Id, feature.Id, StringComparison.OrdinalIgnoreCase)) return OperationResponse<int>.Rejected("duplicate feature id '" + feature.Id + "'");
        _features.Add(feature);
        Trace("FeatureRegistry: added '" + feature.Id + "' (" + _features.Count + " features)");
        return OperationResponse<int>.Success(_features.Count);
    }

    /// Starts / stops features whose gate changed. Expected outcome: Success with the number of features changed (0 = nothing to do),
    /// Failed when at least one Start / Stop threw or reported it did not finish (the reason names them).
    public OperationResponse<int> Refresh()
    {
        int changed = 0;
        string failed = null;
        foreach (var f in _features)
        {
            bool want = SafeGate(f);
            bool running = _running.Contains(f.Id);
            if (want == running) continue;
            bool ok = want ? Run(f, true) : Run(f, false);
            if (!ok) { failed = failed == null ? f.Id : failed + ", " + f.Id; continue; }
            if (want) _running.Add(f.Id); else _running.Remove(f.Id);
            changed++;
            Trace("FeatureRegistry: '" + f.Id + "' " + (want ? "started" : "stopped"));
        }
        if (failed != null) return OperationResponse<int>.Failed("could not switch: " + failed);
        return OperationResponse<int>.Success(changed, changed == 0 ? "no change" : changed + " feature(s) switched");
    }

    /// Stops every running feature (mod unload). Expected outcome: Success with the number stopped; Failed when one could not stop.
    public OperationResponse<int> StopAll()
    {
        int stopped = 0;
        string failed = null;
        foreach (var f in _features)
        {
            if (!_running.Contains(f.Id)) continue;
            if (!Run(f, false)) { failed = failed == null ? f.Id : failed + ", " + f.Id; continue; }
            _running.Remove(f.Id);
            stopped++;
        }
        if (failed != null) return OperationResponse<int>.Failed("could not stop: " + failed);
        return OperationResponse<int>.Success(stopped);
    }

    bool SafeGate(IFeature f)
    {
        bool on;
        try { on = f.IsEnabled(); }
        catch (Exception e)       // last resort: a broken gate counts as "off", never takes the other features down
        {
            Trace("FeatureRegistry: gate of '" + f.Id + "' threw " + e.GetType().Name + ": " + e.Message + " - treated as off");
            on = BrokenGateIsOn;
        }
        return on;
    }

    bool Run(IFeature f, bool start)
    {
        bool finished;
        try
        {
            finished = start ? f.Start() : f.Stop();
            if (!finished) Trace("FeatureRegistry: '" + f.Id + "' " + (start ? "Start" : "Stop") + " did not finish");
        }
        catch (Exception e)       // last resort: report the feature, keep the registry consistent
        {
            Trace("FeatureRegistry: '" + f.Id + "' " + (start ? "Start" : "Stop") + " threw " + e.GetType().Name + ": " + e.Message);
            finished = ThrownMeansFinished;
        }
        return finished;
    }

    void Trace(string line) { if (_trace != null) _trace(line); }
}