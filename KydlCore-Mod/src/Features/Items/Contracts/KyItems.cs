using System;
using System.Collections.Generic;
using System.Linq;
using Kydl.Common;
using KydlCore.Features.Items.Contracts;
using KydlCore.Features.Items.Domain;
using KydlCore.Integrations.JotunnAdapter;
using UnityEngine;

namespace KydlCore
{
    /// <summary>
    /// The KY-Items registry: the user's own items and pieces (spec 5, PLAN T-040). Public API of KydlCore. Each mod CONTRIBUTES its items with
    /// <see cref="Contribute"/> (namespaced id, owner, display name, prefab, recipe, callbacks; duplicates rejected) - MIGRATION_DECISIONS B13: the
    /// registry is Jotunn-free, the game side (adding prefabs / recipes, the build category) is the <see cref="IKyGameSide"/> adapter in
    /// src/Integrations/Jotunn. LEGACY sources stay until every mod is ported: Jotunn's ModRegistry (com.kydldev.* items / pieces), the KY-Items
    /// build category and <see cref="Register"/>. One entry per prefab name (the first source wins: Contribute, Register, then legacy).
    /// </summary>
    public static class KyItems
    {
        /// <summary>The build menu category of the user's pieces; never change the string.</summary>
        public const string CategoryName = KyCategory.Name;

        static readonly KyItemsModel Model = new KyItemsModel();
        static readonly ContributionBook Book = new ContributionBook();
        static readonly List<KyEntry> Contributed = new List<KyEntry>();      // Contribute calls; they survive every refresh
        static readonly List<KyEntry> Explicit = new List<KyEntry>();         // legacy Register calls
        static bool _hooked;

        /// <summary>The game side (Jotunn adapter). Internal: swapped only by tests / a later adapter.</summary>
        internal static IKyGameSide Game = new JotunnItems();

        /// <summary>True once the registry has read the game at least once (after the game registered the items / pieces). Before that the list is empty.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Raised (on the main thread) when the set of the user's prefabs changed: the first read that found some, a later one, a Contribute / Withdraw.</summary>
        public static event Action Changed;

        /// <summary>Every entry, in the order found. Never null.</summary>
        public static IReadOnlyList<KyEntry> All { get { return Model.All; } }

        /// <summary>The entry of a prefab name (case ignored), or null.</summary>
        public static KyEntry Find(string prefab) { return Model.Find(prefab); }

        /// <summary>The entry of a contribution id ("stevethetroll:slippers", case ignored), or null.</summary>
        public static KyEntry FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Contributed.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The entries one mod registered (its GUID, case ignored).</summary>
        public static IEnumerable<KyEntry> ByMod(string modGuid) { return Model.ByMod(modGuid); }

        /// <summary>True for "com.kydldev.*" GUIDs. A mod leaves its own GUID out itself when it lists the others' items.</summary>
        public static bool IsUserGuid(string guid) { return KyItemRules.IsUserGuid(guid); }

        // Stryker disable all : game code (prefabs, the game side adapter): the rules are unit-tested in KyItemRules / ContributionBook; in-game tests KC-xx
        /// <summary>
        /// Adds a mod's item / piece to KY-Items (spec 5). The id is "&lt;modid&gt;:&lt;item&gt;" of the owning mod; a duplicate id or an already
        /// contributed prefab is rejected (also across mods). With RegisterInGame the prefab is added to the game too. Expected outcome: Accepted with the
        /// normalised id, or Rejected with the reason (logged as a warning - a mod's mistake, never silent).
        /// </summary>
        public static KyResult Contribute(KyContribution c)
        {
            if (c == null) return Refuse("", "no contribution");
            if (c.Prefab == null) return Refuse(c.Id, ContributionBook.NoPrefab);
            var valid = KyItemRules.ValidateId(c.Id, c.ModGuid);
            if (!valid.IsSuccess) return Refuse(c.Id, valid.Reason);
            string id = valid.Value;
            var added = Book.Add(new ContributionRecord(id, c.ModGuid, c.Prefab.name));
            if (!added.IsSuccess) return Refuse(id, added.Reason);
            if (c.RegisterInGame)
            {
                KyResult game = Game.Register(c, id);
                if (!game.Accepted) { Book.Remove(id); return Refuse(id, "the game side refused it: " + game.Reason); }
            }
            KyEntry entry = Make(c.Prefab, c.ModGuid, c.ModGuid, c.Kind, c.Tags, "Contribute");
            entry.Id = id;
            if (!string.IsNullOrEmpty(c.DisplayName)) entry.NameToken = c.DisplayName;
            Contributed.Add(entry);
            bool listed = Model.Add(entry);
            ModLog.Verbose("KyItems: contribution '" + id + "' (" + c.Kind + " '" + c.Prefab.name + "') from " + c.ModGuid + (c.RegisterInGame ? ", added to the game" : "") + (listed ? "" : " (prefab already listed by a legacy source)"));
            try { if (c.OnRegistered != null) c.OnRegistered(c.Prefab); }
            catch (Exception e) { ModLog.Warn("KyItems: the OnRegistered callback of '" + id + "' threw: " + e); }        // last resort: a mod's callback must not break the registry
            if (Ready) RaiseChanged();
            return KyResult.Ok(id, listed ? "" : "listed already");
        }

        /// <summary>Removes a contribution (a mod's feature was switched off: no stale entry stays). Expected outcome: true when the id is not listed any more.</summary>
        public static bool Withdraw(string id)
        {
            KyEntry e = FindById(id);
            Book.Remove(id);
            if (e != null)
            {
                Contributed.Remove(e);
                var next = new KyItemsModel();
                foreach (KyEntry x in Model.All) if (!ReferenceEquals(x, e)) next.Add(x);
                Model.ReplaceWith(next);
                ModLog.Verbose("KyItems: contribution '" + id + "' withdrawn");
                if (Ready) RaiseChanged();
            }
            return FindById(id) == null && !Book.Contains(id);
        }

        /// <summary>
        /// LEGACY (before the spec API; use <see cref="Contribute"/>): adds a prefab of the calling mod to the registry. Expected outcome: true when it
        /// is new; false for a null prefab, an empty mod GUID, or a prefab name that is already listed.
        /// </summary>
        public static bool Register(GameObject prefab, string modGuid, KyKind kind, params string[] tags)
        {
            bool refused = prefab == null || string.IsNullOrEmpty(modGuid) || Explicit.Any(e => string.Equals(e.Prefab, prefab.name, StringComparison.OrdinalIgnoreCase));
            bool added = !refused;
            if (refused) ModLog.Verbose("KyItems: Register refused (" + (prefab == null ? "no prefab" : prefab.name) + ", mod '" + modGuid + "')");
            else
            {
                KyEntry entry = Make(prefab, modGuid, modGuid, kind, tags, "Register");
                Explicit.Add(entry);
                added = Model.Add(entry);
                ModLog.Verbose("KyItems: '" + prefab.name + "' registered by " + modGuid + " (legacy Register)" + (added ? "" : " (already found by another source)"));
                if (added && Ready) RaiseChanged();
            }
            return added;
        }

        /// <summary>The "KY-Items" build category, or null while no mod has registered it (Core never makes an empty one).</summary>
        public static Piece.PieceCategory? Category { get { return Game.Category; } }

        /// <summary>Called once by the ItemsFeature: reads the game each time it has registered all items and pieces. Expected outcome: true when hooked.</summary>
        internal static bool Init()
        {
            if (_hooked) return _hooked;
            _hooked = Game.Hook(Refresh);
            ModLog.Verbose("KyItems: game side " + Game.GetType().Name + (_hooked ? " hooked" : " NOT hooked"));
            return _hooked;
        }

        /// <summary>Rebuilds the list (contributions first, then legacy Register, then the legacy game scan) and raises Changed when the set of prefabs changed.</summary>
        internal static void Refresh(string why)
        {
            var next = new KyItemsModel();
            foreach (KyEntry e in Contributed) next.Add(e);
            foreach (KyEntry e in Explicit) next.Add(e);
            int legacy = 0, category = 0;
            foreach (KyFound f in Game.LegacyScan())
                if (f.Prefab != null && KyItemRules.IsUserGuid(f.ModGuid) && next.Add(Make(f.Prefab, f.ModGuid, f.ModName, f.Kind, null, "Jotunn"))) legacy++;
            Piece.PieceCategory? ky = Category;
            if (ky.HasValue)
                foreach (GameObject go in Game.PiecesIn(ky.Value))
                    if (next.Add(Make(go, "", "build category " + CategoryName, KyKind.Piece, null, "category"))) category++;
            bool changed = Model.ReplaceWith(next);
            bool first = !Ready;
            Ready = true;
            ModLog.Verbose("KyItems: " + Model.All.Count + " prefab(s) (" + Contributed.Count + " contributed, " + Explicit.Count + " registered, " + legacy + " legacy from the game side, "
                           + category + " more in the '" + CategoryName + "' category) - " + why + (ky.HasValue ? "" : " - category not registered") + (changed ? " - changed" : ""));
            if (changed || (first && Model.All.Count > 0)) RaiseChanged();
        }

        static KyResult Refuse(string id, string reason)
        {
            ModLog.Warn("KyItems: contribution '" + id + "' rejected - " + reason);
            return KyResult.Rejected(id, reason);
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
            catch (NullReferenceException e) { ModLog.Verbose("KyItems: name / icon of '" + go.name + "' could not be read: " + e.Message); }
            ModLog.Verbose("KyItems: '" + go.name + "' (" + kind + ") from " + modName + " via " + source);
            return new KyEntry { Prefab = go.name, PrefabObject = go, ModGuid = guid ?? "", ModName = modName ?? "", Kind = kind, NameToken = token, Icon = icon, Tags = tags ?? new string[0], Source = source };
        }

        static void RaiseChanged()
        {
            ModLog.Verbose("KyItems: Changed (" + Model.All.Count + " prefab(s))");
            try { Changed?.Invoke(); }
            catch (Exception e) { ModLog.Warn("KyItems: a Changed handler threw: " + e); }      // last resort: one consumer must not break the others
        }
        // Stryker restore all
    }
}
