using System;
using BepInEx.Logging;
using KydlCore.Features.Diagnostics.Domain;

namespace KydlCore.Features.Diagnostics.Infrastructure;

/// <summary>
/// Listens to every BepInEx log line (any thread) for the flight recorder's triggers: DrakesTestLab's test results (a failed test = a dump,
/// a passed one = a timeline note) and the end of each test, and errors written by a KydlDev mod. It only hands the line on
/// (<see cref="FlightHost"/> queues it); it never logs itself (that would call this listener again).
/// </summary>
internal sealed class FlightListener : ILogListener
{
    /// <summary>Called by BepInEx for every log line.</summary>
    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        if (eventArgs == null || eventArgs.Source == null)
        {
            return;
        }

        try
        {
            Handle(eventArgs.Source.SourceName, eventArgs.Level, Convert.ToString(eventArgs.Data));
        }
        catch (InvalidOperationException)
        {
            // the recorder is stopping (plugin unload)
        }
        catch (Exception)
        {
            // last resort: a recorder bug must never break the game's logging
        }
    }

    static void Handle(string source, LogLevel level, string text)
    {
        if (source == TriggerRules.LabSource)
        {
            if (TriggerRules.TryLabResult(source, text, out string test, out bool failed, out string why))
            {
                FlightHost.LabResult(test, failed, why);
            }
            else if (TriggerRules.IsLabReset(source, text))
            {
                FlightHost.LabReset();
            }

            return;
        }

        if ((level & (LogLevel.Error | LogLevel.Fatal)) != 0 && FlightHost.IsKydlMod(source))
        {
            FlightHost.ModError(source, text);
        }
    }

    /// <summary>Nothing to release.</summary>
    public void Dispose()
    {
        // the listener holds no resources
    }
}
