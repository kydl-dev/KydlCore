using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using KydlCore.Common;
using KydlCore.Features.Diagnostics.Infrastructure;
using UnityEngine;

namespace KydlCore.Features.Diagnostics.Contracts;

/// <summary>
/// KydlCore's flight recorder, the public API (user, 2026-11-06: diagnostics from the start, not after a failure). Always on, in memory:
/// it keeps the last couple of minutes of every object a mod tracks and writes them to BepInEx\cache\KydlCore\flight\ only when something
/// goes wrong (a failed TestLab test, a CHECK OFF, an error of a KydlDev mod, or <see cref="Dump"/>).
/// <para>Use it for every creature or object the mod moves or controls (one line where it is created or taken over):</para>
/// <code>FlightRecorder.Track(troll, "Spencer 1", () =&gt; "state " + state + ", order " + order);</code>
/// <para>A watchdog that sees something wrong calls <c>FlightRecorder.Dump("Spencer 1 has not moved for 10 s")</c>.
/// ModLog.State / Decision / Every lines go into the recorder's timeline by themselves.</para>
/// </summary>
public static class FlightRecorder
{
    /// <summary>
    /// Tracks an object: every sample records its position, speed, health, AI target, network owner, whether its zone is loaded, and the text
    /// <paramref name="facts"/> returns (the mod's own state; keep it short and cheap - it runs once per sample). Tracking the same object
    /// again (or a new object with the same saved data, e.g. after the zone reloaded) keeps its history. When the object unloads, the
    /// recorder follows its saved data.
    /// </summary>
    /// <returns>Whether it is tracked afterwards (false: null object, recorder off, or 200 objects already tracked).</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Track(Component target, string label, Func<string> facts = null) =>
        FlightHost.Track(ModContexts.For(Assembly.GetCallingAssembly()), target, label, facts);

    /// <summary>Stops tracking an object (its history is dropped). Returns whether it was tracked.</summary>
    public static bool Untrack(Component target) => FlightHost.Untrack(target);

    /// <summary>Adds a line to the recorder's timeline (for something that is neither a state change nor a decision). Returns whether it was stored.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Note(string text) => FlightHost.Note(ModContexts.For(Assembly.GetCallingAssembly()).Name, text);

    /// <summary>
    /// Writes a dump now (a watchdog saw something wrong): the newest sample is taken first. At most one per mod every 5 s; off the main
    /// thread it is written on the next frame.
    /// </summary>
    /// <returns>The dump file's path, or null (queued, too soon, or the per-game-start limit is reached).</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string Dump(string reason) => FlightHost.DumpNow(ModContexts.For(Assembly.GetCallingAssembly()).Name, reason);

    /// <summary>The newest dump file of this game start, or null.</summary>
    public static string LastDumpPath => FlightHost.LastDumpPath;

    /// <summary>How many objects are tracked now (all mods).</summary>
    public static int TrackedCount => FlightHost.TrackedCount;
}
