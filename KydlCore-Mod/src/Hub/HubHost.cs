using System;
using BepInEx;
using BepInEx.Bootstrap;
using Kydl.Common;
using Kydl.Ui;
using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore
{
    /// <summary>
    /// The GAME side of the KY hub (PLAN T-034): the key, the window, the input lock, the list of loaded KydlDev mods. What the window looks like is
    /// <see cref="HubView"/> (Unity project, shown by the preview too), what it lists is <see cref="HubModel"/> (pure, tested); mods talk to it
    /// only through <see cref="KyHub"/>. Escape closes the top window (the lock makes the game ignore Escape while it is open, like TextInput).
    /// </summary>
    internal static class HubHost
    {
        const string WindowId = KyWindowId.Hub;

        static WindowStack Stack { get { return KyWindows.Stack; } }       // the one stack of the hub and the KyUi windows
        static HubView _view;
        static ConfigPage _config;                // the auto Settings page that is on screen (disposed when another page replaces it)

        internal static bool IsOpen { get { return _view != null; } }

        /// <summary>Every frame (Plugin.Update): the hub key opens / closes the hub, Escape closes the top window. Returns whether the hub is open.</summary>
        internal static bool Tick()
        {
            if (KeyCapture.Blocks) return IsOpen;          // a key is being chosen for a setting (maybe this very key): it must not toggle the hub
            KeyCode key = ModSettings.HubKey != null ? ModSettings.HubKey.Value : KeyCode.None;
            if (!IsOpen)
            {
                if (key == KeyCode.None || !ZInput.GetKeyDown(key, true)) return false;
                if (Chat.instance != null && Chat.instance.HasFocus() || global::Console.IsVisible() || Menu.IsVisible())
                {
                    ModLog.Verbose("HubHost: key pressed while chat / console / game menu is open - not opened");
                    return false;
                }
                Open(null, null);
                return IsOpen;
            }
            if (key != KeyCode.None && ZInput.GetKeyDown(key, true)) Close("key");      // Escape: KyWindows.Tick closes the front window
            return IsOpen;
        }

        /// <summary>Opens the hub (or just selects: it is open already) and shows `mod` / `tab`; null = where it was.</summary>
        internal static void Open(string mod, string tab)
        {
            SyncMods();
            if (mod != null && !KyHub.Model.Select(mod, tab))
                ModLog.Warn("HubHost: no mod '" + mod + "' / tab '" + tab + "' in the hub - showing the last page");
            if (IsOpen) { Stack.BringToFront(WindowId); ModLog.Verbose("HubHost: already open, selection changed"); return; }
            var root = UiKit.Root;
            if (root == null) { ModLog.Warn("HubHost: UI Toolkit is not available, the hub cannot open"); return; }
            KyWindows.EnsureWired();
            _view = new HubView(KyHub.Model, k => Loc.T(k), Page, () => Close("X"));
            root.Add(_view.Root);
            Stack.Open(WindowId);
            ModLog.Verbose("HubHost: opened, " + KyHub.Model.ModCount + " mod(s), page " + KyHub.Model.SelectedMod + " / " + KyHub.Model.SelectedTab);
        }

        internal static void Close(string why)
        {
            if (_view == null) return;
            _view.Root.RemoveFromHierarchy();
            _view.Dispose();
            _view = null;
            DisposeConfig();
            Stack.Close(WindowId);
            ModLog.Verbose("HubHost: closed (" + why + ")");
        }

        /// <summary>KyTab.Refresh: build the page again when it is the one on screen.</summary>
        internal static void Refresh(string mod, string tab)
        {
            if (_view != null && KyHub.Model.SelectedMod == mod && KyHub.Model.SelectedTab == tab) _view.Refresh();
        }

        // Row 1 = every loaded plugin whose GUID starts "com.kydldev." (BepInEx Chainloader); a SetModTitle name wins over the plugin's own name.
        static void SyncMods()
        {
            int before = KyHub.Model.ModCount;
            foreach (PluginInfo info in Chainloader.PluginInfos.Values)
            {
                string guid = info.Metadata.GUID;
                if (!HubModel.IsHubGuid(guid)) continue;
                KyHub.Model.AddMod(guid, KyHub.TitleOf(guid) ?? info.Metadata.Name);
            }
            ModLog.Verbose("HubHost: mods listed " + before + " -> " + KyHub.Model.ModCount);
        }

        // The page factory of HubView: the mod's own page, or the auto Settings page (all config entries) of a mod without tabs.
        static VisualElement Page(string mod, string tab)
        {
            DisposeConfig();
            VisualElement page = null;
            if (KyHub.Model.IsAutoTab(mod)) page = Kydl.Ui.UiLog.Within(mod, () => AutoPage(mod));
            else page = KyHub.BuildPage(mod, tab);
            KyHub.RaiseTabShown(mod, tab);
            return page;
        }

        static VisualElement AutoPage(string mod)
        {
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(mod, out info) || info.Instance == null)
            {
                ModLog.Warn("HubHost: no loaded plugin '" + mod + "' for its Settings page");
                return null;
            }
            try
            {
                _config = new ConfigPage(info.Instance.Config);
                return _config.Root;
            }
            catch (Exception e)
            {
                ModLog.Error("HubHost: the Settings page of " + mod + " failed: " + e);
                return null;
            }
        }

        static void DisposeConfig()
        {
            if (_config == null) return;
            _config.Dispose();
            _config = null;
        }
    }
}
