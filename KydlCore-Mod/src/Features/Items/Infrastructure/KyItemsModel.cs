using System;
using System.Collections.Generic;
using System.Linq;
using KydlCore.Features.Items.Contracts;

namespace KydlCore.Features.Items.Infrastructure
{
    /// <summary>
    /// The pure part of the KY-Items registry (PLAN T-040, DESIGN 6): the list of entries with one entry per prefab name, the lookups and the rule
    /// "which mod GUID is the user's". No Unity calls and no logging (like WindowStack / HubModel): <see cref="KyItems"/> finds the prefabs in the game
    /// and puts them here.
    /// </summary>
    internal sealed class KyItemsModel
    {
        internal const string GuidPrefix = "com.kydldev.";

        readonly List<KyEntry> _all = new List<KyEntry>();
        readonly Dictionary<string, KyEntry> _byPrefab = new Dictionary<string, KyEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every entry in the order they were added.</summary>
        internal IReadOnlyList<KyEntry> All { get { return _all; } }

        /// <summary>
        /// Adds an entry. Expected outcome: true when it is new; false (nothing changes) for null, an empty prefab name, or a prefab name that is
        /// already there (the first entry wins: the same prefab found by two ways is listed once).
        /// </summary>
        internal bool Add(KyEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Prefab) || _byPrefab.ContainsKey(entry.Prefab)) return false;
            _byPrefab[entry.Prefab] = entry;
            _all.Add(entry);
            return true;
        }

        /// <summary>The entry of a prefab name (case ignored), or null.</summary>
        internal KyEntry Find(string prefab)
        {
            if (prefab == null) return null;
            KyEntry e;
            _byPrefab.TryGetValue(prefab, out e);                 // e stays null when the name is unknown
            return e;
        }

        /// <summary>The entries of one mod GUID (case ignored), in order; none for null / empty.</summary>
        internal IEnumerable<KyEntry> ByMod(string modGuid)
        {
            if (string.IsNullOrEmpty(modGuid)) return Enumerable.Empty<KyEntry>();
            return _all.Where(e => string.Equals(e.ModGuid, modGuid, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Makes this list the one in <paramref name="next"/>. Expected outcome: true when the set of prefab names changed (a new one or a gone one), so
        /// the registry raises Changed; false when only the same prefabs were found again (their entries are still replaced by the fresh ones).
        /// </summary>
        internal bool ReplaceWith(KyItemsModel next)
        {
            bool changed = !new HashSet<string>(_byPrefab.Keys, StringComparer.OrdinalIgnoreCase).SetEquals(next._byPrefab.Keys);
            _all.Clear(); _byPrefab.Clear();
            foreach (KyEntry e in next._all.ToList()) Add(e);
            return changed;
        }

        /// <summary>The user's mods: "com.kydldev.*" (case ignored) with a name after the prefix. Consumers leave out their own GUID themselves.</summary>
        internal static bool IsUserGuid(string guid) { return KydlCore.Features.Items.Domain.KyItemRules.IsUserGuid(guid); }
    }
}
