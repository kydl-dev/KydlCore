using System;
using Kydl.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore.Ui
{
    /// <summary>
    /// The KY hub window (F6, DESIGN 5, PLAN T-032): title bar, row 1 = one tab per KydlDev mod, a gold line, row 2 = the selected mod's tabs,
    /// then the page of the selected tab. WHAT is shown comes from the pure <see cref="HubModel"/>; the page content comes from `page(modGuid, tabId)`
    /// (the game side builds config pages and KyHub tabs, the Unity preview fakes them). Compiled into the mod AND shown by the Unity preview
    /// (MenuPreview, kind Hub); no game types here.
    /// Look (D-048, user feedback 2026-10-30, D-055): tabs are as big as the buttons (same padding); mod names (row 1) are red text, settings tabs
    /// (row 2) have a red button border and gold text; the selected tab has a rounded gold border, a faint gold halo around it (UI Toolkit has no
    /// drop shadow) and sits 3 px higher; a thin red border while the pointer is over a tab. The title, the tab names and the page start on one
    /// vertical line (TextX). Under the settings tabs the same gold line as under the mod names (same element, Rule()), then every page starts with a line saying which page it is. Rows wrap onto further lines (up to 2 shown;
    /// scroll bar + arrows for more: TabRows / TabPaging, T-035); a title wider than the window gets an ellipsis.
    /// The view redraws when the model fires Changed; Dispose unhooks it.
    /// </summary>
    internal sealed class HubView : IDisposable
    {
        static readonly Color NameGold = new Color32(0xF3, 0xAD, 0x54, 0xFF);        // #F3AD54: title, mod names, settings names
        static readonly Color SelectedFill = new Color32(0x0F, 0x2C, 0x5E, 0xFF); // #0F2C5E: background of the selected tab
        const float UnderW = 3f;              // the bottom border of a selected / hovered tab
        const float TabEdge = 6f;             // space between the window edge and the first tab
        const float HaloPad = 2f, BorderW = 2f, PadH = 14f;     // space kept for the tab edge (the names start at TextX), side padding (the buttons' 14)
        const float TextX = TabEdge + HaloPad + BorderW + PadH; // where the first tab name starts = where the title and the page start

        readonly HubModel _model;
        readonly Func<string, string> _text;
        readonly Func<string, string, VisualElement> _page;
        readonly UiScroll _body;
        readonly TabRows _row1, _row2;
        readonly Action _onChanged;
        string _shownMod, _shownTab;
        bool _built;

        /// <summary>The window; the caller adds it to UiKit.Root.</summary>
        internal VisualElement Root { get; private set; }

        /// <summary>Builds the window for `model`. `text` turns a text key into the shown text; `page` builds the content of a tab (null = an empty page); `onClose` is the X button.</summary>
        internal HubView(HubModel model, Func<string, string> text, Func<string, string, VisualElement> page, Action onClose)
        {
            _model = model; _text = text; _page = page;
            Root = UiKit.Window(text("kydlcore_hub_title"), "", onClose, out _body);
            var head = Root[0];                                    // the title bar of UiKit.Window: slimmer, and its text on the same line as the tabs
            var titleLabel = head.Q<Label>();                      // the window title in the same colour as the tab names
            if (titleLabel != null) titleLabel.style.color = NameGold;
            head.style.paddingTop = 6; head.style.paddingBottom = 6; head.style.paddingLeft = TextX;
            _body.Content.style.paddingLeft = TextX;

            var rows = new VisualElement { name = "kydl-hub-rows" };
            rows.style.flexShrink = 0; rows.style.marginLeft = TabEdge; rows.style.marginRight = TabEdge; rows.style.marginTop = 8;
            _row1 = new TabRows("kydl-hub-row1");
            _row2 = new TabRows("kydl-hub-row2");
            rows.Add(_row1.Root); rows.Add(Rule("kydl-hub-line")); rows.Add(_row2.Root); rows.Add(Rule("kydl-hub-page-line"));   // both lines are the same element: gold, 2 px, full width
            Root.Insert(1, rows);                                  // between the title bar and the page area

            _onChanged = Rebuild;
            _model.Changed += _onChanged;
            Rebuild();
            UiLog.Verbose("HubView: built, " + _model.ModCount + " mod(s), selected " + _model.SelectedMod + " / " + _model.SelectedTab);
        }

        /// <summary>Builds the shown page again (KyTab.Refresh): the page factory is asked once more.</summary>
        internal void Refresh()
        {
            if (!_built) return;
            ShowPage(_shownMod, _shownTab);
            UiLog.Verbose("HubView: page refreshed");
        }

        /// <summary>Stops listening to the model (call when the window is destroyed).</summary>
        public void Dispose()
        {
            _model.Changed -= _onChanged;
            UiLog.Verbose("HubView: disposed");
        }

        static VisualElement Rule(string name)
        {
            var l = new VisualElement { name = name };
            l.style.height = 2; l.style.backgroundColor = UiKit.Gold; l.style.marginTop = 2; l.style.marginBottom = 6; l.style.flexShrink = 0;
            return l;
        }

        // Redraws both rows and, when the selected mod / tab changed, the page.
        void Rebuild()
        {
            string mod = _model.SelectedMod, tab = _model.SelectedTab;
            _row1.Clear();
            foreach (string guid in _model.ModGuids())
            {
                string g = guid;
                _row1.Add(Tab(_model.ModTitle(g), g == mod, UiKit.SectionSize, false, () => _model.SelectMod(g)), g == mod);
            }
            _row2.Clear();
            if (mod != null)
                foreach (string id in _model.TabIds(mod))
                {
                    string t = id;
                    _row2.Add(Tab(_model.TabTitle(mod, t), t == tab, UiKit.ButtonSize, true, () => _model.SelectTab(t)), t == tab);
                }
            if (_built && mod == _shownMod && tab == _shownTab) { UiLog.Verbose("HubView: rows redrawn, page stays " + mod + " / " + tab); return; }
            _built = true; _shownMod = mod; _shownTab = tab;
            ShowPage(mod, tab);
        }

        void ShowPage(string mod, string tab)
        {
            var c = _body.Content;
            c.Clear();
            VisualElement page = mod == null ? null : _page(mod, tab);
            if (mod != null) c.Add(UiControls.Note(string.Format(_text("kydlcore_hub_page_of"), _model.ModTitle(mod), _model.TabTitle(mod, tab))));
            if (page != null) c.Add(page);
            else c.Add(UiControls.Note(_text(mod == null ? "kydlcore_hub_empty" : "kydlcore_hub_page_empty")));
            _body.ScrollTo(0f);
            UiLog.Verbose("HubView: page " + (mod ?? "(no mod)") + " / " + (tab ?? "-") + (page != null ? " built" : " empty"));
        }

        // One tab (user 2026-10-30, D-059): no border at rest, names in NameGold; the selected tab has a red bottom border and the blue fill; a hovered
        // tab shows only the red bottom border. Row 1 and row 2 look alike, only the size differs.
        static VisualElement Tab(string title, bool selected, int size, bool boxed, Action pick)
        {
            var b = new Button(pick) { text = title };
            b.style.fontSize = size; b.style.color = NameGold; b.style.flexShrink = 1; b.style.minWidth = 0; b.style.maxWidth = Length.Percent(100f);
            b.style.overflow = Overflow.Hidden; b.style.textOverflow = TextOverflow.Ellipsis; b.style.whiteSpace = WhiteSpace.NoWrap;
            b.style.marginLeft = 0; b.style.marginRight = 6; b.style.marginTop = 3; b.style.marginBottom = 3;
            b.style.backgroundColor = selected ? SelectedFill : Color.clear;
            UiKit.Pad(b, 6, PadH); UiKit.Radius(b, 0);
            UiKit.Borders(b, 0, Color.clear);
            b.style.borderBottomWidth = UnderW;
            b.style.borderBottomColor = selected ? UiKit.TabColor : Color.clear;
            if (!selected)
            {
                b.RegisterCallback<PointerEnterEvent>(e => b.style.borderBottomColor = UiKit.TabColor);
                b.RegisterCallback<PointerLeaveEvent>(e => b.style.borderBottomColor = Color.clear);
            }
            return b;
        }    }
}
