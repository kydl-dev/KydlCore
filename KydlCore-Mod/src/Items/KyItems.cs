using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using Jotunn.Utils;
using Kydl.Common;
using UnityEngine;

namespace KydlCore
{
    /// <summary>
    /// The registry of the user's own items and pieces (PLAN T-040, DESIGN 6; user 2026-10-26: items of his mods show up in the chest decor
    /// categories under "KY-Items", only when such a mod exists). Ported from ChestDecor's <c>KyItems</c> and generalised. Three sources, one entry per
    /// prefab name (the first source wins): Jotunn's ModRegistry (every CustomItem / CustomPiece of a "com.kydldev.*" mod), every hammer piece in the
    /// "KY-Items" build category (whoever registered it) and explicit <see cref="Register"/> calls. It is read again when Jotunn has registered all
    /// items / pieces, and raises <see cref="Changed"/> when the set of prefabs changed. Core never creates the build category itself (D-014): it only
    /// reads it. The rules (one entry per prefab, lookups, which GUID is the user's) are the pure <see cref="KyItemsModel"/>.
    /// </summary>
    public static class KyItems
    {
        /// <summary>The build menu category of the user's pieces; never change the string.</summary>
        public const string CategoryName = KyCategory.Name;

        static readonly KyItemsModel Model = new KyItemsModel();
        static readonly List<KyEntry> Explicit = new List<KyEntry>();      // Register calls; they survive every refresh
        static bool _subscribed;

        /// <summary>True once the registry has read the game at least once (after Jotunn registered the items / pieces). Before that the list is empty.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Raised (on the main thread) when the set of the user's prefabs changed: the first read that found some, a later one, a Register.</summary>
        public static event Action Changed;

        /// <summary>Every entry, in the order found. Never null.</summary>
        public static IReadOnlyList<KyEntry> All { get { return Model.All; } }

        /// <summary>The entry of a prefab name (case ignored), or null.</summary>
        public static KyEntry Find(string prefab) { return Model.Find(prefab); }

        /// <summary>The entries one mod registered (its GUID, case ignored).</summary>
        public static IEnumerable<KyEntry> ByMod(string modGuid) { return Model.ByMod(modGuid); }

        /// <summary>True for "com.kydldev.*" GUIDs. A mod leaves its own GUID out itself when it lists the others' items.</summary>
        public static bool IsUserGuid(string guid) { return KyItemsModel.IsUserGuid(guid); }

        // Stryker disable all : game code (Jotunn managers, ObjectDB, prefabs): no unit test can run it; in-game tests KC-xx (TEST_QUALITY.md)
        /// <summary>
        /// Adds a prefab of the calling mod to the registry (also one that is no hammer piece and not made with Jotunn). Expected outcome: true when it
        /// is new (Changed is raised once the registry is Ready); false for a null prefab, a mod GUID that is empty, or a prefab name that is already listed.
        /// </summary>
        public static bool Register(GameObject prefab, string modGuid, KyKind kind, params string[] tags)
        {
            if (prefab == null || string.IsNullOrEmpty(modGuid) || Explicit.Any(e => string.Equals(e.Prefab, prefab.name, StringComparison.OrdinalIgnoreCase)))
            {
                ModLog.Verbose("KyItems: Register refused (" + (prefab == null ? "no prefab" : prefab.name) + ", mod '" + modGuid + "')");
                return false;
            }
            KyEntry entry = Make(prefab, modGuid, modGuid, kind, tags, "Register");
            Explicit.Add(entry);
            bool added = Model.Add(entry);
            ModLog.Verbose("KyItems: '" + prefab.name + "' registered by " + modGuid + (added ? "" : " (already found by another source)"));
            if (added && Ready) RaiseChanged();
            return added;
        }

        /// <summary>The "KY-Items" build category, or null while no mod has registered it (Core never makes an empty one).</summary>
        public static Piece.PieceCategory? Category
        {
            get
            {
                try
                {
                    return PieceManager.Instance.GetPieceCategory(CategoryName);       // null while no mod has registered it
                }
                catch (Exception e) { ModLog.Verbose("KyItems: the '" + CategoryName + "' build category could not be read: " + e.Message); return null; }
            }
        }

        /// <summary>Called once from the plugin: reads the game each time Jotunn has registered all items and all pieces.</summary>
        internal static void Init()
        {
            if (_subscribed) return;
            _subscribed = true;
            try
            {
                ItemManager.OnItemsRegistered += () => Refresh("Jotunn registered the items");
                PieceManager.OnPiecesRegistered += () => Refresh("Jotunn registered the pieces");
                ModLog.Verbose("KyItems: waiting for Jotunn's OnItemsRegistered / OnPiecesRegistered");
            }
            catch (Exception e) { ModLog.Warn("KyItems: Jotunn's registered events are not available (" + e.Message + ")"); }
        }

        /// <summary>Reads the three sources again and raises Changed when the set of prefabs changed (also the first time it finds any).</summary>
        internal static void Refresh(string why)
        {
            var next = new KyItemsModel();
            int items = 0, pieces = 0, category = 0;
            try
            {
                foreach (var ci in ModRegistry.GetItems())
                    if (ci != null && ci.ItemPrefab != null && ci.SourceMod != null && KyItemsModel.IsUserGuid(ci.SourceMod.GUID) &&
                        next.Add(Make(ci.ItemPrefab, ci.SourceMod.GUID, ci.SourceMod.Name, KyKind.Item, null, "Jotunn"))) items++;
                foreach (var cp in ModRegistry.GetPieces())
                    if (cp != null && cp.PiecePrefab != null && cp.SourceMod != null && KyItemsModel.IsUserGuid(cp.SourceMod.GUID) &&
                        next.Add(Make(cp.PiecePrefab, cp.SourceMod.GUID, cp.SourceMod.Name, KyKind.Piece, null, "Jotunn"))) pieces++;
            }
            catch (TypeLoadException e) { ModLog.Warn("KyItems: Jotunn's ModRegistry is not available (" + e.Message + ")"); }
            catch (MissingMethodException e) { ModLog.Warn("KyItems: Jotunn's ModRegistry is not available (" + e.Message + ")"); }
            catch (Exception e) { ModLog.Warn("KyItems: reading Jotunn's ModRegistry failed: " + e.Message); }

            Piece.PieceCategory? ky = Category;
            if (ky.HasValue)
                foreach (GameObject go in HammerPieces())
                {
                    var piece = go.GetComponent<Piece>();
                    if (piece != null && piece.m_category == ky.Value && next.Add(Make(go, "", "build category " + CategoryName, KyKind.Piece, null, "category"))) category++;
                }
            foreach (KyEntry e in Explicit) next.Add(e);

            bool changed = Model.ReplaceWith(next);
            bool first = !Ready;
            Ready = true;
            ModLog.Verbose("KyItems: " + Model.All.Count + " prefab(s) (" + items + " item(s) and " + pieces + " piece(s) of com.kydldev.* mods, " + category +
                           " more piece(s) in the '" + CategoryName + "' category, " + Explicit.Count + " registered) - " + why + (ky.HasValue ? "" : " - category not registered") +
                           (changed ? " - changed" : ""));
            if (changed || (first && Model.All.Count > 0)) RaiseChanged();
        }

        // The hammer's build pieces (the KY-Items category is a hammer category); empty while the game data is not there yet.
        static IEnumerable<GameObject> HammerPieces()
        {
            try
            {
                GameObject hammer = ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab("Hammer");
                ItemDrop drop = hammer == null ? null : hammer.GetComponent<ItemDrop>();
                PieceTable table = drop == null ? null : drop.m_itemData.m_shared.m_buildPieces;
                return table == null ? new List<GameObject>() : table.m_pieces.Where(p => p != null).ToList();
            }
            catch (Exception e) { ModLog.Verbose("KyItems: the hammer's pieces could not be read: " + e.Message); return new List<GameObject>(); }
        }

        static KyEntry Make(GameObject go, string guid, string modName, KyKind kind, string[] tags, string source)
        {
            string token = go.name;
            Sprite icon = null;
            try
            {
                ItemDrop drop = go.GetComponent<ItemDrop>();
                Piece piece = go.GetComponent<Piece>();
                if (drop != null) { var shared = drop.m_itemData.m_shared; token = shared.m_name; icon = shared.m_icons != null && shared.m_icons.Length > 0 ? shared.m_icons[0] : null; }
                else if (piece != null) { token = piece.m_name; icon = piece.m_icon; }
            }
            catch (Exception e) { ModLog.Verbose("KyItems: name / icon of '" + go.name + "' could not be read: " + e.Message); }
            ModLog.Verbose("KyItems: '" + go.name + "' (" + kind + ") from " + modName);
            return new KyEntry { Prefab = go.name, PrefabObject = go, ModGuid = guid ?? "", ModName = modName ?? "", Kind = kind, NameToken = token, Icon = icon, Tags = tags ?? new string[0], Source = source };
        }

        static void RaiseChanged()
        {
            ModLog.Verbose("KyItems: Changed (" + Model.All.Count + " prefab(s))");
            try { Changed?.Invoke(); }
            catch (Exception e) { ModLog.Warn("KyItems: a Changed handler threw: " + e); }
        }
        // Stryker restore all
    }
}
