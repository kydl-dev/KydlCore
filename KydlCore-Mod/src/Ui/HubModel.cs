using System;
using System.Collections.Generic;
using System.Linq;

namespace KydlCore.Ui
{
    /// <summary>
    /// What the KY hub (F6) shows and what is selected (DESIGN 5, PLAN T-031). Pure C#: no Unity, no BepInEx, no logging, so it is unit tested;
    /// the view (T-032) draws it and the glue code (T-034) feeds it from Chainloader.PluginInfos and KyHub.AddTab.
    ///  * Row 1 = one tab per mod added with AddMod, ordered by title (ordinal, ignoring case), then by guid (D-028). Only guids that start with
    ///    "com.kydldev." count (IsHubGuid); the same guid is in the row once (adding it again updates its title).
    ///  * Row 2 = the tabs the selected mod registered with AddTab, ordered by `order`, then by registration order. A mod with NO registered tab
    ///    shows ONE automatic tab (AutoTabId, "Settings"), which the view fills with every config entry of the mod (D-023).
    ///  * Selection: while at least one mod exists, exactly one mod and exactly one of its tabs are selected; with no mod nothing is. The first
    ///    mod added is selected; a mod that sorts before it later does not steal the selection. Selecting a mod shows the tab last selected in it
    ///    (the first tab when none, or when that tab is gone). Removing the selected mod / tab moves the selection to the first one left.
    ///  * Changed fires once after every call that really changed something (rows, titles or selection); a call that changes nothing is silent.
    /// Ids and guids are compared ordinally (case sensitive); null and "" are not valid. Main thread only (it is UI code).
    /// </summary>
    internal sealed class HubModel
    {
        /// <summary>Every KydlDev plugin guid starts with this (the hub lists no other mod).</summary>
        public const string ModPrefix = "com.kydldev.";
        /// <summary>The id of the one automatic tab of a mod that registered no tab.</summary>
        public const string AutoTabId = "settings";
        /// <summary>The title of that automatic tab.</summary>
        public const string AutoTabTitle = "Settings";

        sealed class Tab { public string Id; public string Title; public int Order; public int Seq; }

        readonly Dictionary<string, string> _mods = new Dictionary<string, string>();             // guid -> row-1 title
        readonly Dictionary<string, List<Tab>> _tabs = new Dictionary<string, List<Tab>>();      // guid -> registered tabs (registration order)
        readonly Dictionary<string, string> _lastTab = new Dictionary<string, string>();          // guid -> tab last selected in that mod
        static readonly List<Tab> NoTabs = new List<Tab>();                                        // "registered nothing"; never changed
        int _seq;
        bool _dirty;

        /// <summary>Fires after a call changed the rows, a title or the selection (once per call).</summary>
        public event Action Changed;

        /// <summary>The guid of the selected row-1 tab, or null when there is no mod.</summary>
        public string SelectedMod { get; private set; }

        /// <summary>The id of the selected row-2 tab of the selected mod, or null when there is no mod.</summary>
        public string SelectedTab { get; private set; }

        /// <summary>Number of mods in row 1.</summary>
        public int ModCount { get { return _mods.Count; } }

        /// <summary>Does the hub list a mod with this guid? ("com.kydldev." + something; "000.kydldev.GameProbe" and other mods: no.)</summary>
        public static bool IsHubGuid(string guid)
        {
            return !string.IsNullOrEmpty(guid) && guid.Length > ModPrefix.Length && guid.StartsWith(ModPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Adds a mod to row 1, or updates its title when it is there. A blank title shows the guid. False for a guid the hub does not list.</summary>
        public bool AddMod(string guid, string title)
        {
            if (!IsHubGuid(guid)) return false;
            string shown = string.IsNullOrWhiteSpace(title) ? guid : title;
            string old;
            if (!_mods.TryGetValue(guid, out old) || old != shown) { _mods[guid] = shown; _dirty = true; }
            Settle();
            return _mods.ContainsKey(guid);
        }

        /// <summary>Removes a mod from row 1 (its registered tabs stay, in case the plugin comes back). False when it was not there.</summary>
        public bool RemoveMod(string guid)
        {
            if (string.IsNullOrEmpty(guid) || !_mods.Remove(guid)) return false;
            _dirty = true;
            Settle();
            return !_mods.ContainsKey(guid);
        }

        /// <summary>The guids of row 1 in display order (a copy).</summary>
        public string[] ModGuids()
        {
            return _mods.OrderBy(m => m.Value, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Key).ToArray();
        }

        /// <summary>The row-1 title of a mod, or null when it is not in row 1.</summary>
        public string ModTitle(string guid)
        {
            string title = null;
            if (!string.IsNullOrEmpty(guid)) _mods.TryGetValue(guid, out title);
            return title;
        }

        /// <summary>Registers a row-2 tab of a mod (KyHub.AddTab), or updates its title / order when the id is already registered. A blank title shows the id.</summary>
        public bool AddTab(string modGuid, string id, string title, int order)
        {
            if (string.IsNullOrEmpty(modGuid) || string.IsNullOrEmpty(id)) return false;
            string shown = string.IsNullOrWhiteSpace(title) ? id : title;
            List<Tab> list;
            if (!_tabs.TryGetValue(modGuid, out list)) { list = new List<Tab>(); _tabs[modGuid] = list; }
            var tab = list.FirstOrDefault(t => t.Id == id);
            if (tab == null) { list.Add(new Tab { Id = id, Title = shown, Order = order, Seq = _seq++ }); _dirty = true; }
            else if (tab.Title != shown || tab.Order != order) { tab.Title = shown; tab.Order = order; _dirty = true; }
            Settle();
            return list.Any(t => t.Id == id);
        }

        /// <summary>Removes a registered tab. False when it was not registered. A mod left with no tab shows the automatic one.</summary>
        public bool RemoveTab(string modGuid, string id)
        {
            List<Tab> list = Registered(modGuid);                 // NoTabs for a null / unknown mod: nothing to remove
            if (list.RemoveAll(t => t.Id == id) == 0) return false;
            _dirty = true;
            Settle();
            return list.All(t => t.Id != id);
        }

        /// <summary>The row-2 tab ids of a mod in display order; just { AutoTabId } when the mod registered none (also for a mod not in row 1).</summary>
        public string[] TabIds(string modGuid)
        {
            List<Tab> list = Registered(modGuid);
            if (list.Count == 0) return new[] { AutoTabId };
            return list.OrderBy(t => t.Order).ThenBy(t => t.Seq).Select(t => t.Id).ToArray();
        }

        /// <summary>The title of a row-2 tab, or null when the mod has no such tab.</summary>
        public string TabTitle(string modGuid, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            List<Tab> list = Registered(modGuid);
            if (list.Count == 0) return id == AutoTabId ? AutoTabTitle : null;
            var tab = list.FirstOrDefault(t => t.Id == id);
            return tab == null ? null : tab.Title;
        }

        /// <summary>Is the mod's only tab the automatic "Settings" one (it registered no tab)?</summary>
        public bool IsAutoTab(string modGuid)
        {
            return Registered(modGuid).Count == 0;
        }

        /// <summary>Row-1 click: selects the mod and shows the tab last selected in it (the first one when none). False for a mod not in row 1; nothing changes then.</summary>
        public bool SelectMod(string guid) { return Select(guid, null); }

        /// <summary>Row-2 click: selects a tab of the selected mod. False when there is no such tab; nothing changes then.</summary>
        public bool SelectTab(string id) { return SelectedMod != null && !string.IsNullOrEmpty(id) && Select(SelectedMod, id); }

        /// <summary>
        /// Selects a mod and one of its tabs (KyHub.Open): tabId null = the tab last selected in that mod (the first one when none).
        /// False when the mod is not in row 1 or the tab does not exist; nothing changes then.
        /// </summary>
        public bool Select(string guid, string tabId)
        {
            if (string.IsNullOrEmpty(guid) || !_mods.ContainsKey(guid)) return false;
            string tab = tabId;
            if (tab == null) tab = Pick(guid);
            else if (!TabIds(guid).Contains(tab)) return false;
            if (SelectedMod != guid || SelectedTab != tab) { SelectedMod = guid; SelectedTab = tab; _dirty = true; }
            _lastTab[guid] = tab;
            Settle();
            // Stryker disable once Logical : always true here, the two assignments above ran and Settle keeps a valid selection
            return SelectedMod == guid && SelectedTab == tab;
        }

        // The registered tabs of a mod (NoTabs for a mod that has none or an invalid guid).
        List<Tab> Registered(string modGuid)
        {
            List<Tab> list = null;
            if (!string.IsNullOrEmpty(modGuid)) _tabs.TryGetValue(modGuid, out list);
            return list ?? NoTabs;
        }

        // The tab to show in a mod: the tab last selected there (the selected tab of the selected mod is always that one), else the first one.
        string Pick(string mod)
        {
            string[] ids = TabIds(mod);
            string last = null;
            _lastTab.TryGetValue(mod, out last);
            return last != null && ids.Contains(last) ? last : ids[0];
        }

        // After every change: keep "exactly one mod and one of its tabs selected" true, then tell the listeners once.
        void Settle()
        {
            string[] mods = ModGuids();
            string mod = SelectedMod != null && _mods.ContainsKey(SelectedMod) ? SelectedMod : (mods.Length == 0 ? null : mods[0]);
            string tab = null;
            if (mod != null)
            {
                tab = Pick(mod);
                _lastTab[mod] = tab;
            }
            if (mod != SelectedMod || tab != SelectedTab) { SelectedMod = mod; SelectedTab = tab; _dirty = true; }
            if (!_dirty) return;
            _dirty = false;
            if (Changed != null) Changed();
        }
    }
}
