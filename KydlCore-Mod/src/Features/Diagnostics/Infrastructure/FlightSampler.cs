using System;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace KydlCore.Features.Diagnostics.Infrastructure;

/// <summary>
/// The game side of the flight recorder: reads what a sample needs from the running game (main thread only). Everything here is generic
/// (any creature or object); a mod adds its own facts through the function it passes to FlightRecorder.Track.
///  * Environment: frame rate, the game's simulation area (near / total zones - a lower graphics setting shrank it on 2026-11-05 and far
///    trolls froze), the Unity quality level, the local player's position.
///  * An object: position, health, the AI's target, whether it is alerted, who owns it on the network, whether its zone is loaded, and the
///    distance to the local player.
/// </summary>
internal static class FlightSampler
{
    // MonsterAI keeps its target private (STT reads it the same way, TrollGuard)
    static readonly AccessTools.FieldRef<MonsterAI, Character> TargetOf = AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");

    /// <summary>One environment line: "fps 78 | sim near 2 total 4 | quality 3 High | player (-21, 51, 69)".</summary>
    internal static string Environment(double fps, int tracked)
    {
        var sb = new StringBuilder();
        sb.Append("fps ").Append(fps.ToString("0", CultureInfo.InvariantCulture));
        sb.Append(" | sim ").Append(Simulation());
        int q = QualitySettings.GetQualityLevel();
        string[] names = QualitySettings.names;
        sb.Append(" | quality ").Append(q).Append(q >= 0 && q < names.Length ? " " + names[q] : "");
        Player p = Player.m_localPlayer;
        sb.Append(" | player ").Append(p != null ? Pos(p.transform.position) : "none");
        sb.Append(" | tracked ").Append(tracked);
        return sb.ToString();
    }

    /// <summary>The game's simulation area in zones ("near 2 total 4"), or "-" before the network is up.</summary>
    internal static string Simulation()
    {
        if (ZNet.instance == null)
        {
            return "-";
        }

        SimulationDistance sim = ZNet.instance.GetSyncedSimulationDistance();
        return "near " + sim.NearSimulationDistance + " total " + sim.TotalSimulationDistance + (sim.IsClassic ? " classic" : "");
    }

    /// <summary>The generic facts of a loaded object: health, AI target, alerted, network owner, zone loaded, distance to the player.</summary>
    internal static string Facts(Character character, MonsterAI ai, ZNetView view, Vector3 position)
    {
        var sb = new StringBuilder();
        if (character != null)
        {
            sb.Append("hp ").Append(character.GetHealth().ToString("0", CultureInfo.InvariantCulture)).Append('/')
              .Append(character.GetMaxHealth().ToString("0", CultureInfo.InvariantCulture));
            if (character.IsDead())
            {
                sb.Append(" DEAD");
            }
        }

        if (ai != null)
        {
            Character target = TargetOf(ai);
            Sep(sb).Append("target ").Append(target != null ? target.name : "none");
            if (ai.IsAlerted())
            {
                sb.Append(", alerted");
            }
        }

        if (view != null && view.IsValid())
        {
            Sep(sb).Append("owner ").Append(view.IsOwner() ? "me" : view.GetZDO().GetOwner().ToString(CultureInfo.InvariantCulture));
        }

        return AppendWhere(sb, position).ToString();
    }

    /// <summary>The facts of an object that is not loaded: only its saved data (ZDO) is left.</summary>
    internal static string UnloadedFacts(ZDO zdo, Vector3 position)
    {
        var sb = new StringBuilder("NOT LOADED (saved data only)");
        sb.Append(", owner ").Append(zdo.GetOwner() == 0L ? "nobody" : zdo.GetOwner().ToString(CultureInfo.InvariantCulture));
        return AppendWhere(sb, position).ToString();
    }

    static StringBuilder AppendWhere(StringBuilder sb, Vector3 position)
    {
        if (ZoneSystem.instance != null && !ZoneSystem.instance.IsZoneLoaded(position))
        {
            Sep(sb).Append("zone NOT loaded");
        }

        Player p = Player.m_localPlayer;
        if (p != null)
        {
            Sep(sb).Append(Vector3.Distance(p.transform.position, position).ToString("0", CultureInfo.InvariantCulture)).Append(" m from the player");
        }

        return sb;
    }

    static StringBuilder Sep(StringBuilder sb) => sb.Length > 0 ? sb.Append(", ") : sb;

    static string Pos(Vector3 v) =>
        "(" + v.x.ToString("0", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0", CultureInfo.InvariantCulture) + ", " + v.z.ToString("0", CultureInfo.InvariantCulture) + ")";

    /// <summary>A mod's own facts (its function may throw: that is written into the sample, never thrown on).</summary>
    internal static string ModFacts(Func<string> facts)
    {
        if (facts == null)
        {
            return "";
        }

        try
        {
            string s = facts();
            return string.IsNullOrEmpty(s) ? "" : " | " + s;
        }
        catch (NullReferenceException e)
        {
            return " | (the mod's facts failed: " + e.Message + ")";
        }
        catch (Exception e)          // last resort: foreign code
        {
            return " | (the mod's facts failed: " + e.GetType().Name + ": " + e.Message + ")";
        }
    }
}
