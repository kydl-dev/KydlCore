using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KydlCore.Common;

/// <summary>
/// SHARED. Learning a build piece the way the game does it (user, 2026-11-04: "every new item the player learns the recipe of is
/// displayed in the normal way of learning something"): the piece goes into the character's known recipes and the game shows its
/// "new piece" unlock popup. A mod decides WHEN (e.g. HaveRequirements postfix for the build menu + a periodic check that calls
/// <see cref="Learn" />); this class only knows the game side.
/// </summary>
public static class PieceLearning
{
    private static readonly AccessTools.FieldRef<Player, HashSet<string>> Recipes =
        AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownRecipes");

    private static readonly AccessTools.FieldRef<Player, HashSet<string>> Materials =
        AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");

    private static MethodInfo _addKnownPiece;

    /// Is this piece in the character's known recipes? Expected outcome: true once learned.
    public static bool KnowsPiece(Player p, Piece piece)
    {
        return p != null && piece != null && Recipes(p).Contains(piece.m_name);
    }

    /// Has the character discovered this item (prefab name, e.g. "Tar", "Iron")? Expected outcome: true after the first pick-up.
    public static bool KnowsMaterial(Player p, string itemPrefab)
    {
        if (p == null || ObjectDB.instance == null)
        {
            return false;
        }

        GameObject go = ObjectDB.instance.GetItemPrefab(itemPrefab);
        ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
        return drop != null && Materials(p).Contains(drop.m_itemData.m_shared.m_name);
    }

    /// Learns the piece with the game's own popup. Expected outcome: true when it was learned NOW (false: unknown prefab, already known, or the game call failed).
    public static bool Learn(Player p, string piecePrefab)
    {
        if (p == null || ZNetScene.instance == null)
        {
            return false;
        }

        GameObject go = ZNetScene.instance.GetPrefab(piecePrefab);
        Piece piece = go != null ? go.GetComponent<Piece>() : null;
        if (piece == null || KnowsPiece(p, piece))
        {
            return false;
        }

        try
        {
            if (_addKnownPiece == null)
            {
                _addKnownPiece = AccessTools.Method(typeof(Player), "AddKnownPiece", new[] { typeof(Piece) });
            }

            if (_addKnownPiece != null)
            {
                _addKnownPiece.Invoke(p, new object[] { piece });
                return KnowsPiece(p, piece);
            }
        }
        catch (Exception e)
        {
            ModLog.Warn("PieceLearning: Player.AddKnownPiece failed (" + e.GetType().Name + ": " + e.Message + "), using the popup directly");
        }

        Recipes(p).Add(piece.m_name);
        if (MessageHud.instance != null)
        {
            MessageHud.instance.QueueUnlockMsg(piece.m_icon, "$msg_newpiece", piece.m_name);
        }

        return true;
    }
}
