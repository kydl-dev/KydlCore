namespace KydlCore.Features.Diagnostics.Domain;

/// <summary>
/// One sample of one tracked object: when (seconds on the recorder's clock), where (metres), how fast it moved since the sample before
/// (metres per second) and the facts read at that moment (for example "hp 900/900, target Greyling, owner me"). Pure data.
/// </summary>
internal readonly struct FlightSample
{
    /// <summary>Creates a sample.</summary>
    internal FlightSample(double time, double x, double y, double z, double speed, string facts)
    {
        Time = time;
        X = x;
        Y = y;
        Z = z;
        Speed = speed;
        Facts = facts ?? "";
    }

    /// <summary>Seconds on the recorder's clock.</summary>
    internal double Time { get; }

    /// <summary>World position, metres.</summary>
    internal double X { get; }

    /// <summary>World position (height), metres.</summary>
    internal double Y { get; }

    /// <summary>World position, metres.</summary>
    internal double Z { get; }

    /// <summary>Metres per second since the previous sample (0 for the first one).</summary>
    internal double Speed { get; }

    /// <summary>What was read about the object at that moment; never null.</summary>
    internal string Facts { get; }
}
