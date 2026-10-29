using Kydl.Common;
using Kydl.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore.Ui
{
    /// <summary>
    /// The mod's menu in game ([General] MenuKey): opens / closes with the key, Escape closes; the character does not move while it is open.
    /// Only the GAME side lives here (keys, input lock, game data). What the menu shows is built by MainMenuView (Unity project,
    /// KydlCore-Unity/Assets/KydlCore_UI/Runtime), which the Unity preview shows too - change the look there.
    /// </summary>
    internal static class MainMenu
    {
        const string LockName = "KydlCore.MainMenu";
        static VisualElement _window;

        internal static bool IsOpen { get { return _window != null; } }

        /// Every frame (Plugin.Update). Expected outcome: returns whether the menu is open after this frame.
        internal static bool Tick()
        {
            var key = ModSettings.MenuKey != null ? ModSettings.MenuKey.Value : KeyCode.None;
            if (!IsOpen)
            {
                if (key == KeyCode.None || !ZInput.GetKeyDown(key, true)) return IsOpen;
                if (Chat.instance != null && Chat.instance.HasFocus() || global::Console.IsVisible() || Menu.IsVisible())
                {
                    ModLog.Verbose("MainMenu: key pressed while chat / console / game menu is open - not opened");
                    return IsOpen;
                }
                Open();
                return IsOpen;
            }
            if (ZInput.GetKeyDown(key, true) || ZInput.GetKeyDown(KeyCode.Escape, true)) Close("key");
            return IsOpen;
        }

        static void Open()
        {
            var root = UiKit.Root;
            if (root == null) { ModLog.Warn("MainMenu: UI Toolkit is not available, the menu cannot open"); return; }
            _window = MainMenuView.Build(k => Loc.T(k), () => Close("X"));
            root.Add(_window);
            UiInputLock.Hold(LockName);
            ModLog.Verbose("MainMenu: opened");
        }

        static void Close(string why)
        {
            if (_window != null) _window.RemoveFromHierarchy();
            _window = null;
            UiInputLock.Release(LockName);
            ModLog.Verbose("MainMenu: closed (" + why + ")");
        }
    }
}
