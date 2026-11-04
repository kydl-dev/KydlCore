using System.Linq;
using KydlCore.Bootstrap;
using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using KydlCore.Features.Hub.Infrastructure;
using KydlCore.Features.Items.Contracts;
using KydlCore.Presentation.Hub.Pages;
using KydlCore.Ui;
using UnityEngine.UIElements;

namespace KydlCore.Presentation.Hub.Registration;

/// <summary>
/// KydlCore's own tabs in the KY hub (PLAN T-041, DESIGN 6, open question U1): "Settings" (its own config, the same page as the automatic one, which a
/// mod loses as soon as it registers any tab) and "KY-Items" (the list of the user's items / pieces found by <see cref="KyItems"/>). The KY-Items tab
/// exists only while the registry has entries (user: shown only when one of his mods with items is installed). Game / UI code: tested in game.
/// </summary>
internal static class KyItemsTab
{
    internal const string SettingsId = "settings", ItemsId = "ky-items";
    const int ItemsOrder = 10;

    /// <summary>Registers the Settings tab and follows the registry: the KY-Items tab appears and disappears with its entries.</summary>
    /// Expected outcome: true when the Settings tab is registered.
    internal static bool Init()
    {
        var settings = KyHub.AddTab(Plugin.ModGuid, SettingsId, "Settings", () => KyHub.ConfigEntries(Plugin.Instance.Config), 0);
        KyItems.Changed -= Sync;
        KyItems.Changed += Sync;
        Sync();
        ModLog.Verbose("KyItemsTab: Settings tab " + (settings != null ? "registered" : "FAILED") + ", following KyItems.Changed");
        return settings != null;
    }

    static void Sync()
    {
        bool any = KyItems.All.Count > 0;
        if (any) KyHub.AddTab(Plugin.ModGuid, ItemsId, "KY-Items", Build, ItemsOrder);
        else KyHub.RemoveTab(Plugin.ModGuid, ItemsId);
        ModLog.Verbose("KyItemsTab: " + (any ? "KY-Items tab shown (" + KyItems.All.Count + " prefab(s))" : "no entries, KY-Items tab hidden"));
        if (any) HubHost.Refresh(Plugin.ModGuid, ItemsId);
    }

    // The page: the user's prefabs grouped by mod, text at the settings page size (D-061).
    static VisualElement Build()
    {
        var page = new VisualElement { name = "kydl-kyitems-page" };
        page.style.flexShrink = 0;
        var groups = KyItems.All.GroupBy(e => string.IsNullOrEmpty(e.ModName) ? e.ModGuid : e.ModName).ToList();
        page.Add(UiControls.Note(KyItems.All.Count + " prefab(s) of " + groups.Count + " source(s). Items and pieces the KydlDev mods contribute; pieces in the '" + KyItems.CategoryName + "' build category are included."));
        foreach (var group in groups)
        {
            page.Add(UiControls.Heading(group.Key));
            foreach (KyEntry entry in group) page.Add(Row(entry));
        }
        ConfigPage.ScaleFonts(page);
        ModLog.Verbose("KyItemsTab: page built, " + KyItems.All.Count + " prefab(s) in " + groups.Count + " group(s)");
        return page;
    }

    static VisualElement Row(KyEntry entry)
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0; r.style.marginTop = 2; r.style.marginBottom = 2;
        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.style.width = 28; icon.style.height = 28; icon.style.flexShrink = 0; icon.style.marginRight = 8;
        if (entry.Icon != null) icon.style.backgroundImage = new StyleBackground(entry.Icon);
        r.Add(icon);
        var name = UiKit.Label(entry.DisplayName, UiKit.NameSize, UiKit.NameColor);
        name.style.flexGrow = 1; name.style.whiteSpace = WhiteSpace.NoWrap;
        r.Add(name);
        r.Add(UiKit.Label(entry.Kind + "  " + (string.IsNullOrEmpty(entry.Id) ? entry.Prefab : entry.Id), UiKit.SmallSize, UiKit.TextDim));
        return r;
    }
}
