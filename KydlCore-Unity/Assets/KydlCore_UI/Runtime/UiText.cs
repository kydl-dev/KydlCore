using System.Collections.Generic;

namespace KydlCore.Ui
{
    /// <summary>
    /// Player-facing text of the mod, English only (user rule: no other languages). Shared by the game (registered with Jotunn in Plugin.Awake through KydlCore.Common.Loc)
    /// and the Unity preview (English). Every key starts with "kydlcore_" so it never clashes with another mod.
    /// </summary>
    internal static class UiText
    {
        internal static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "kydlcore_menu_title", "KydlCore" },
            { "kydlcore_menu_footer", "Esc to close" },
            { "kydlcore_menu_section", "Example" },
            { "kydlcore_menu_note", "This is the starting menu of the mod. Replace its content in MainMenuView." },
            { "kydlcore_menu_example_switch", "Example switch" },
            { "kydlcore_menu_example_slider", "Example value" },
            { "kydlcore_menu_close", "Close" },
            { "kydlcore_hub_title", "KydlCore Control Panel" },
            { "kydlcore_hub_page_of", "Page: {0} / {1}" },
            { "kydlcore_hub_empty", "No KydlDev mod is loaded." },
            { "kydlcore_hub_page_empty", "This tab has nothing to show yet." },
        };


        /// English text (the Unity preview; the game reads the same table through Jotunn).
        internal static string English(string key)
        {
            string s;
            return En.TryGetValue(key, out s) ? s : key;
        }
    }
}
