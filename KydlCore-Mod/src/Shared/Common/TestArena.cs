using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace KydlCore.Common;

/// <summary>
/// The shared TestLab arena (MIGRATION_DECISIONS C26, lessons TESTLAB_PLAYBOOK "Geometry", from SteveTheTroll D-171 / D-172; user 2026-11-02: "spawn player at
/// least 50 m away from spawn", 2026-11-03: "spawning the player further away from spawn point"). Every test of every mod starts with the mod's `<mod>.arena`
/// hook, which calls <see cref="MovePlayer"/>: the player stands on the SAME open spot, <see cref="ArenaMetres"/> from the lab's start (away from the lab house
/// and the boss-trophy stones), faces world +z (so a test's dz > 0 is in FRONT of the player), god mode on, velocity and fall height reset. The spot is
/// chosen once per game session: of 16 directions, the flattest dry 40 x 40 m area. Game code (compiled into every mod; only test hooks call it).
/// </summary>
public static class TestArena
{
    public const float ArenaMetres = 70f, AreaHalf = 20f, MaxWater = 0.1f;
    /// Spawn creatures / objects at least this far from the player, in front of it (lessons: a creature next to / behind the player walks into it).
    public const float MinSpawnMetres = 16f;
    /// AppDomain keys of the session-wide arena (SteveTheTroll's own TestArena uses the same keys until it is ported).
    public const string StartKey = "Kydl.TestArena.Start", SpotKey = "Kydl.TestArena.Spot", RankedKey = "Kydl.TestArena.Ranked", LanesKey = "Kydl.TestArena.Lanes";
    const string RankZeroOwner = "SteveTheTroll";
    static Vector3[] _ranked;
    static bool _known;
    static Vector3 _start;
    static readonly System.Collections.Generic.Dictionary<string, Vector3> Spots = new();     // the test spot of each mod (by mod name)
    static readonly AccessTools.FieldRef<Character, float> MaxAirAltitude = AccessTools.FieldRefAccess<Character, float>("m_maxAirAltitude");

    /// The chosen spot (zero before the first MovePlayer).
    public static Vector3 Spot { [MethodImpl(MethodImplOptions.NoInlining)] get { return Spots.TryGetValue(ModContexts.For(Assembly.GetCallingAssembly()).Name, out var s) ? s : default; } }

    /// Moves the local player to the arena spot. Expected outcome: "arena at (x, y, z), N m from the lab start, god mode True" or "no player".
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string MovePlayer()
    {
        var ctx = ModContexts.For(Assembly.GetCallingAssembly());
        var p = Player.m_localPlayer;
        if (p == null) return "no player";
        if (!_known)
        {
            _known = true;
            // ONE lab start for every mod in the session, but each mod its OWN spot (ecosystem runs 20261103-1053 / -1123): sharing one spot put the
            // tar pits / pools / chests of earlier suites into SteveTheTroll's long routes. The 16 directions are ranked by flatness once (shared through the
            // AppDomain; SteveTheTroll's own arena reads the same keys); SteveTheTroll keeps rank 0 (its long-route tests were tuned there, D-171), every
            // other mod takes the next free rank in the order it first asks.
            var domain = System.AppDomain.CurrentDomain;
            if (domain.GetData(StartKey) is Vector3 s0 && domain.GetData(RankedKey) is Vector3[] ranked0) { _start = s0; _ranked = ranked0; }
            else
            {
                _start = domain.GetData(StartKey) is Vector3 st ? st : p.transform.position;     // SteveTheTroll's own arena may have set the start first
                _ranked = Rank(ctx, _start);
                domain.SetData(StartKey, _start);
                domain.SetData(RankedKey, _ranked);
                domain.SetData(SpotKey, _ranked[0]);      // SteveTheTroll's own TestArena reads this one
            }
        }
        if (!Spots.TryGetValue(ctx.Name, out var spot))
        {
            spot = LaneOf(ctx.Name);
            Spots[ctx.Name] = spot;
            ModLog.Info(ctx, "TestArena: lab start " + _start.ToString("F0") + " -> test spot of " + ctx.Name + " " + spot.ToString("F0") + " (" + Flat(spot - _start).ToString("F0") + " m away)");
        }
        Place(p, spot, Quaternion.identity);
        if (!p.InGodMode()) p.SetGodMode(true);
        float away = Flat(p.transform.position - _start);
        ModLog.Check(ctx, "TestArena: distance from the lab start", away, ArenaMetres, 1f, "m");
        return "arena at " + spot.ToString("F0") + ", " + away.ToString("F0") + " m from the lab start, god mode " + p.InGodMode();
    }

    /// A point `dx`, `dz` metres from the player AS IT STANDS NOW, pushed out to at least <see cref="MinSpawnMetres"/> and set on the ground. Throws when the
    /// offset is not in front of the player (a test must spawn in front: dz > 0 after MovePlayer). Expected outcome: the point.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector3 InFront(float dx, float dz)
    {
        var ctx = ModContexts.For(Assembly.GetCallingAssembly());
        var p = Player.m_localPlayer;
        var off = new Vector3(dx, 0f, dz);
        var fwd = p.GetLookDir(); fwd.y = 0f;
        if (Vector3.Dot(off, fwd) <= 0f) throw new System.ArgumentException("offset (" + dx + ", " + dz + ") is not in front of the player - spawn in front (lessons D-172)");
        if (off.magnitude < MinSpawnMetres) off = off.normalized * MinSpawnMetres;
        var at = p.transform.position + off;
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.3f;
        ModLog.Verbose(ctx, "TestArena: spawn point " + off.magnitude.ToString("F1") + " m in front of the player at " + at.ToString("F0"));
        return at;
    }

    // Puts the player there AT ONCE (TeleportTo finishes frames later, after the area loads: a later step could run before it); velocity and fall height reset (D-172).
    static void Place(Player p, Vector3 to, Quaternion rot)
    {
        var body = p.GetComponent<Rigidbody>();
        if (body != null) { body.linearVelocity = Vector3.zero; body.position = to; body.rotation = rot; }
        p.transform.position = to;
        p.transform.rotation = rot;
        p.SetLookDir(rot * Vector3.forward);
        MaxAirAltitude(p) = to.y;
        if (ZNet.instance != null) ZNet.instance.SetReferencePosition(to);
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    /// The lane of a mod: rank 0 for SteveTheTroll, the next free rank (1, 2, ...) for every other mod, kept for the whole session.
    static Vector3 LaneOf(string mod)
    {
        var domain = System.AppDomain.CurrentDomain;
        var lanes = domain.GetData(LanesKey) as System.Collections.Generic.Dictionary<string, int>;
        if (lanes == null) { lanes = new System.Collections.Generic.Dictionary<string, int>(); domain.SetData(LanesKey, lanes); }
        int rank;
        if (!lanes.TryGetValue(mod, out rank))
        {
            rank = mod == RankZeroOwner ? 0 : 1 + lanes.Count(kv => kv.Key != RankZeroOwner);
            lanes[mod] = rank;
        }
        return _ranked[rank % _ranked.Length];
    }

    /// The 16 directions at ArenaMetres from the start, flattest dry 40 x 40 m area first.
    static Vector3[] Rank(ModContext ctx, Vector3 start)
    {
        var zs = ZoneSystem.instance;
        var spots = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, Vector3>>();
        for (int k = 0; k < 16; k++)
        {
            float a = k * Mathf.PI * 2f / 16f;
            var c = start + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ArenaMetres;
            if (zs == null) { spots.Add(new System.Collections.Generic.KeyValuePair<float, Vector3>(k, c)); continue; }
            float lo = float.MaxValue, hi = float.MinValue;
            bool wet = false;
            for (float x = -AreaHalf; x <= AreaHalf; x += 5f)
            for (float z = -AreaHalf; z <= AreaHalf; z += 5f)
            {
                var q = c + new Vector3(x, 0f, z);
                float h = zs.GetGroundHeight(q);
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                if (Floating.GetLiquidLevel(new Vector3(q.x, h, q.z)) - h > MaxWater) wet = true;
            }
            float score = (hi - lo) + (wet ? 1000f : 0f);
            ModLog.Verbose(ctx, "TestArena: direction " + k + " uneven " + (hi - lo).ToString("F1") + " m, water " + wet);
            spots.Add(new System.Collections.Generic.KeyValuePair<float, Vector3>(score, new Vector3(c.x, zs.GetGroundHeight(c) + 0.2f, c.z)));
        }
        return spots.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
    }
}
