using System;
using System.Collections.Generic;
using Kydl.Common;
using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore
{
    /// <summary>A row-2 tab of the KY hub, made by <see cref="KyHub.AddTab"/>.</summary>
    public sealed class KyTab
    {
        /// <summary>The plugin GUID of the mod that owns the tab.</summary>
        public string ModGuid { get; internal set; }
        /// <summary>The tab id, unique inside the mod.</summary>
        public string Id { get; internal set; }
        /// <summary>The shown name.</summary>
        public string Title { get; internal set; }
        /// <summary>The place in row 2 (smaller first; the same order = the order of registering).</summary>
        public int Order { get; internal set; }

        /// <summary>Builds the page again when this tab is the one on screen (the mod's data changed).</summary>
        // Stryker disable once Block : the body only calls the game-side window (nothing to see while no hub is open)
        public void Refresh()
        {
            ModLog.Verbose("KyHub: Refresh " + ModGuid + " / " + Id);
            // Stryker disable once all : only does something while the hub window is open (game)
            HubHost.Refresh(ModGuid, Id);
        }
    }

    /// <summary>
    /// The KY hub (F6): the one menu of every KydlDev mod (DESIGN 3 / 5, PLAN T-034). Row 1 = every loaded plugin whose GUID starts with
    /// "com.kydldev.", row 2 = the tabs that mod registered here; a mod that registers none gets one automatic "Settings" tab with all its config
    /// entries (D-023). A mod never has its own menu key: it adds tabs and, for something tied to a world object, calls <see cref="Open"/>.
    /// Calls may come from any mod's Awake: the tab list exists before the hub window does.
    /// </summary>
    public static class KyHub
    {
        internal static readonly HubModel Model = new HubModel();
        static readonly Dictionary<string, Func<VisualElement>> Builders = new Dictionary<string, Func<VisualElement>>();
        static readonly Dictionary<string, string> Titles = new Dictionary<string, string>();
        static readonly Dictionary<string, KyTab> Tabs = new Dictionary<string, KyTab>();

        /// <summary>Raised when a tab's page is shown (the first time it is opened and every time it is built again): mod GUID, tab id.</summary>
        public static event Action<string, string> TabShown;

        /// <summary>True while the hub window is open.</summary>
        // Stryker disable once all : false without the game window; the real value is checked in game
        public static bool IsOpen { get { return HubHost.IsOpen; } }

        /// <summary>
        /// Adds a row-2 tab of a mod, or updates the title / page builder / order when the id is already there. `build` makes the page content
        /// (called every time the tab is shown). Returns null for a blank GUID or id or a null `build` (a warning is logged).
        /// </summary>
        public static KyTab AddTab(string modGuid, string id, string title, Func<VisualElement> build, int order = 0)
        {
            if (string.IsNullOrEmpty(modGuid) || string.IsNullOrEmpty(id) || build == null)
            {
                ModLog.Warn("KyHub.AddTab: needs a mod GUID, a tab id and a page builder (got '" + modGuid + "', '" + id + "', " + (build == null ? "no builder" : "a builder") + ")");
                return null;
            }
            string key = Key(modGuid, id);
            Builders[key] = build;
            Model.AddTab(modGuid, id, title, order);
            KyTab tab;
            if (!Tabs.TryGetValue(key, out tab)) { tab = new KyTab { ModGuid = modGuid, Id = id }; Tabs[key] = tab; }
            tab.Title = Model.TabTitle(modGuid, id);
            tab.Order = order;
            ModLog.Verbose("KyHub: tab " + modGuid + " / " + id + " '" + tab.Title + "' order " + order);
            return tab;
        }

        /// <summary>Removes a tab of a mod. False when it was not there. A mod left with no tab shows the automatic "Settings" tab again.</summary>
        public static bool RemoveTab(string modGuid, string id)
        {
            bool removed = Model.RemoveTab(modGuid, id);
            string key = Key(modGuid, id);
            Builders.Remove(key);
            Tabs.Remove(key);
            ModLog.Verbose("KyHub: RemoveTab " + modGuid + " / " + id + (removed ? "" : " (was not there)"));
            return removed;
        }

        /// <summary>Sets the row-1 name of a mod (the default is the plugin's name). `icon` is reserved for later and not shown yet.</summary>
        public static void SetModTitle(string modGuid, string title, Sprite icon = null)
        {
            if (!HubModel.IsHubGuid(modGuid)) { ModLog.Warn("KyHub.SetModTitle: '" + modGuid + "' is not a KydlDev mod GUID (com.kydldev.*)"); return; }
            Titles[modGuid] = title;
            if (Model.ModTitle(modGuid) != null) Model.AddMod(modGuid, title);
            ModLog.Verbose("KyHub: title of " + modGuid + " is '" + title + "'");
        }

        // Stryker disable all : Open needs the running game (UI root, plugin list); tested in game
        /// <summary>Opens the hub (or brings it to the front) and shows a mod / tab; both null = where it was last. Needs the game UI: false when it is not there yet.</summary>
        public static void Open(string modGuid = null, string tabId = null)
        {
            ModLog.Verbose("KyHub: Open " + (modGuid ?? "(last)") + " / " + (tabId ?? "(last)"));
            HubHost.Open(modGuid, tabId);
        }
        // Stryker restore all

        /// <summary>Closes the hub window.</summary>
        // Stryker disable once Block : the body only calls the game-side window (nothing to see while no hub is open)
        public static void Close()
        {
            ModLog.Verbose("KyHub: Close");
            // Stryker disable once all : only does something while the hub window is open (game)
            HubHost.Close("KyHub.Close");
        }

        /// <summary>
        /// A ready page of config entries for a mod's own tab: one control per entry of `file` (all sections, or only the named ones), the same controls and
        /// hot reload as the automatic Settings tab. Call it inside the `build` of <see cref="AddTab"/>. Entries tagged Browsable = false are left out.
        /// </summary>
        public static VisualElement ConfigEntries(BepInEx.Configuration.ConfigFile file, params string[] sections)
        {
            ModLog.Verbose("KyHub: ConfigEntries for " + (file == null ? "(no file)" : file.ConfigFilePath) + ", " + (sections == null || sections.Length == 0 ? "all sections" : string.Join(", ", sections)));
            if (file == null) return null;
            // Stryker disable all : needs a real ConfigFile and the UI; tested in game
            var page = new ConfigPage(file, sections);
            page.Root.RegisterCallback<DetachFromPanelEvent>(e => page.Dispose());          // the page stops listening when it leaves the screen
            return page.Root;
            // Stryker restore all
        }

        internal static string TitleOf(string modGuid)
        {
            string t;
            return Titles.TryGetValue(modGuid, out t) ? t : null;
        }

        /// <summary>The page of a registered tab, or null when the mod registered no builder for it. A throwing builder is logged and gives null.</summary>
        internal static VisualElement BuildPage(string modGuid, string id)
        {
            Func<VisualElement> build;
            if (!Builders.TryGetValue(Key(modGuid, id), out build)) return null;
            try { return build(); }
            // Stryker disable once all : the only effect of the handler is the error line (the null result is tested)
            catch (Exception e) { ModLog.Error("KyHub: the page of " + modGuid + " / " + id + " failed: " + e); return null; }
        }

        internal static void RaiseTabShown(string modGuid, string id)
        {
            var handler = TabShown;
            if (handler == null) return;
            foreach (Action<string, string> h in handler.GetInvocationList())
            {
                try { h(modGuid, id); }
                catch (Exception e) { ModLog.Error("KyHub: a TabShown handler failed: " + e); }
            }
        }

        static string Key(string modGuid, string id) { return modGuid + "\n" + id; }
    }
}
