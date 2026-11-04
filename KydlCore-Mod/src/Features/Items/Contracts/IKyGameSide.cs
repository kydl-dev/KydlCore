using System;
using System.Collections.Generic;
using UnityEngine;

namespace KydlCore.Features.Items.Contracts;

/// <summary>A prefab of a KydlDev mod the game side found by itself (legacy: before the mod contributes it explicitly).</summary>
internal readonly struct KyFound
{
    internal GameObject Prefab { get; }
    internal string ModGuid { get; }
    internal string ModName { get; }
    internal KyKind Kind { get; }
    internal KyFound(GameObject prefab, string guid, string name, KyKind kind) { Prefab = prefab; ModGuid = guid ?? ""; ModName = name ?? ""; Kind = kind; }
}

/// <summary>
/// The game-side part of KY-Items (MIGRATION_DECISIONS B13): the registry itself is Jotunn-free; this capability is implemented by the Jotunn
/// adapter (src/Integrations/Jotunn) and could be swapped for another one later.
/// </summary>
internal interface IKyGameSide
{
    /// Calls `refresh` whenever the game has registered its items / pieces. Expected outcome: true when subscribed.
    bool Hook(Action<string> refresh);

    /// The "KY-Items" build category (null while no mod has made it).
    Piece.PieceCategory? Category { get; }

    /// LEGACY (until every mod contributes explicitly): items / pieces of com.kydldev.* mods the game side knows.
    IEnumerable<KyFound> LegacyScan();

    /// The hammer pieces in a build category.
    IEnumerable<GameObject> PiecesIn(Piece.PieceCategory category);

    /// Adds a contribution's prefab to the game. Expected outcome: Accepted when the game side took it.
    KyResult Register(KyContribution contribution, string id);
}
