namespace KydlCore.Features.Diagnostics.Domain;

/// <summary>
/// One line of the flight recorder's timeline: a state change, a decision, a test result, a CHECK OFF, or one environment sample
/// (frame rate, simulation area, quality). Pure data.
/// </summary>
internal readonly struct FlightNote
{
    /// <summary>Creates a note.</summary>
    internal FlightNote(double time, string source, string text)
    {
        Time = time;
        Source = source ?? "";
        Text = text ?? "";
    }

    /// <summary>Seconds on the recorder's clock.</summary>
    internal double Time { get; }

    /// <summary>Who wrote it (a mod name, "TestLab", "env"); never null.</summary>
    internal string Source { get; }

    /// <summary>The text; never null.</summary>
    internal string Text { get; }
}
