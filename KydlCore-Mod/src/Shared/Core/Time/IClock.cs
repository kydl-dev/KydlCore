namespace KydlCore.Core;

/// <summary>Game time for pure code (Domain / Application / Execution must not read UnityEngine.Time). In game: an adapter over Time.time / ZNet time; in tests: <see cref="ManualClock"/>.</summary>
public interface IClock
{
    /// Seconds since the game started (Time.time in game).
    float Now { get; }
}

/// <summary>A clock tests move by hand.</summary>
public sealed class ManualClock : IClock
{
    public float Now { get; private set; }
    public ManualClock(float start = 0f) { Now = start; }
    /// Moves the clock. Expected outcome: Now grew by `seconds` (negative values are ignored).
    public bool Advance(float seconds)
    {
        float before = Now;
        if (seconds > 0f) Now += seconds;
        return Now >= before;
    }
}