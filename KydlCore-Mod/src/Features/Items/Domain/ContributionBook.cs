using System;
using System.Collections.Generic;
using KydlCore.Core;

namespace KydlCore.Features.Items.Domain
{
    /// <summary>One accepted contribution: id, owner, prefab name.</summary>
    internal readonly struct ContributionRecord
    {
        internal string Id { get; }
        internal string ModGuid { get; }
        internal string Prefab { get; }
        internal ContributionRecord(string id, string modGuid, string prefab) { Id = id ?? ""; ModGuid = modGuid ?? ""; Prefab = prefab ?? ""; }
    }

    /// <summary>
    /// Which contribution ids and prefabs are taken (spec 5: duplicate identifiers are rejected, also across mods). One id per prefab and one prefab per id.
    /// Pure: tested in tests/Unit.
    /// </summary>
    internal sealed class ContributionBook
    {
        internal const string NoPrefab = "no prefab";
        readonly Dictionary<string, ContributionRecord> _byId = new Dictionary<string, ContributionRecord>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> _idByPrefab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal int Count { get { return _byId.Count; } }
        internal bool Contains(string id) { return id != null && _byId.ContainsKey(id); }

        /// Expected outcome: Success with the new count; Rejected when the id or the prefab is already taken (the reason names the holder).
        internal OperationResponse<int> Add(ContributionRecord record)
        {
            if (string.IsNullOrEmpty(record.Prefab)) return OperationResponse<int>.Rejected(NoPrefab);
            ContributionRecord holder;
            if (_byId.TryGetValue(record.Id, out holder)) return OperationResponse<int>.Rejected("the id '" + record.Id + "' is already registered by " + holder.ModGuid);
            string otherId;
            if (_idByPrefab.TryGetValue(record.Prefab, out otherId)) return OperationResponse<int>.Rejected("the prefab '" + record.Prefab + "' is already contributed as '" + otherId + "'");
            _byId[record.Id] = record;
            _idByPrefab[record.Prefab] = record.Id;
            return OperationResponse<int>.Success(_byId.Count);
        }

        /// Expected outcome: true when the id is not taken any more (also when it never was).
        internal bool Remove(string id)
        {
            ContributionRecord r;
            if (id != null && _byId.TryGetValue(id, out r)) { _byId.Remove(id); _idByPrefab.Remove(r.Prefab); }
            return !Contains(id);
        }

        /// The record of an id (Prefab "" when unknown).
        internal ContributionRecord Find(string id)
        {
            ContributionRecord r;
            return id != null && _byId.TryGetValue(id, out r) ? r : new ContributionRecord(id, "", "");
        }
    }
}
