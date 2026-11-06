using KydlCore.Common;
using KydlCore.Core;

namespace KydlCore.Features.Diagnostics.Infrastructure;

/// <summary>
/// The flight recorder (user, 2026-11-06). Always on (MIGRATION_DECISIONS B12): it costs one sample per interval and writes nothing until
/// something goes wrong. Start hooks the BepInEx log and ModLog's CHECK OFF / note events; Stop (plugin unload) unhooks them.
/// </summary>
internal sealed class DiagnosticsFeature : IFeature
{
    internal const string FeatureId = "diagnostics";
    const bool AlwaysOn = true;

    public string Id { get { return FeatureId; } }

    public bool IsEnabled() { return AlwaysOn; }

    public bool Start()
    {
        bool started = FlightHost.Start();
        ModLog.Verbose("DiagnosticsFeature: started (flight recorder " + (started ? "recording" : "NOT recording") + ")");
        return started;
    }

    public bool Stop()
    {
        bool stopped = FlightHost.Stop();
        ModLog.Verbose("DiagnosticsFeature: stopped (flight recorder off " + stopped + ")");
        return stopped;
    }
}
