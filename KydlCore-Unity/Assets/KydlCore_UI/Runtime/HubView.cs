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
    /// Look (D-048): tab names in UiKit.TabColor (red in the KydlCore build, PascalCase as given); the selected tab has a rounded gold border, a
    /// faint gold halo around it (UI Toolkit has no drop shadow) and sits 3 px higher than the others; a thin red border while the pointer is over a tab.
    /// Rows wrap onto further lines; scroll bar + paging for more than 2 rows is T-035.
    /// The view redraws when the model fires Changed; Dispose unhooks it.
    /// </summary>
    internal sealed class HubView : IDisposable
    {
        const float HaloAlpha = 0.22f;        // opacity of the gold halo around the selected tab
        const float Lift = 3f;                // how much higher the selected tab stands
        const float Rail = 18f;               // same side space as the window body, so the rows line up with the page

        readonly HubModel _model;
        readonly Func<string, string> _text;
        readonly Func<string, string, VisualElement> _page;
        readonly UiScroll _body;
        readonly VisualElement _row1, _row2;
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

            var rows = new VisualElement { name = "kydl-hub-rows" };
            rows.style.flexShrink = 0; rows.style.marginLeft = Rail; rows.style.marginRight = Rail; rows.style.marginTop = 10;
            _row1 = Row("kydl-hub-row1");
            var line = new VisualElement { name = "kydl-hub-line" };
            line.style.height = 2; line.style.backgroundColor = UiKit.Gold; line.style.marginTop = 2; line.style.marginBottom = 8;
            _row2 = Row("kydl-hub-row2");
            rows.Add(_row1); rows.Add(line); rows.Add(_row2);
            Root.Insert(1, rows);                                  // between the title bar and the page area

            _onChanged = Rebuild;
            _model.Changed += _onChanged;
            Rebuild();
            UiLog.Verbose("HubView: built, " + _model.ModCount + " mod(s), selected " + _model.SelectedMod + " / " + _model.SelectedTab);
        }

        /// <summary>Stops listening to the model (call when the window is destroyed).</summary>
        public void Dispose()
        {
            _model.Changed -= _onChanged;
            UiLog.Verbose("HubView: disposed");
        }

        static VisualElement Row(string name)
        {
            var r = new VisualElement { name = name };
            r.style.flexDirection = FlexDirection.Row; r.style.flexWrap = Wrap.Wrap; r.style.alignItems = Align.FlexStart;
            return r;
        }

        // Redraws both rows and, when the selected mod / tab changed, the page.
        void Rebuild()
        {
            string mod = _model.SelectedMod, tab = _model.SelectedTab;
            _row1.Clear();
            foreach (string guid in _model.ModGuids())
            {
                string g = guid;
                _row1.Add(Tab(_model.ModTitle(g), g == mod, UiKit.SectionSize, () => _model.SelectMod(g)));
            }
            _row2.Clear();
            if (mod != null)
                foreach (string id in _model.TabIds(mod))
                {
                    string t = id;
                    _row2.Add(Tab(_model.TabTitle(mod, t), t == tab, UiKit.ButtonSize, () => _model.SelectTab(t)));
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
            if (page != null) c.Add(page);
            else c.Add(UiControls.Note(_text(mod == null ? "kydlcore_hub_empty" : "kydlcore_hub_page_empty")));
            _body.ScrollTo(0f);
            UiLog.Verbose("HubView: page " + (mod ?? "(no mod)") + " / " + (tab ?? "-") + (page != null ? " built" : " empty"));
        }

        // One tab: a halo ring (selected only) around a button. The unselected ones are lowered by `Lift`, so the selected one stands out.
        static VisualElement Tab(string title, bool selected, int size, Action pick)
        {
            var halo = new VisualElement();
            halo.style.paddingLeft = 3; halo.style.paddingRight = 3; halo.style.paddingTop = 3; halo.style.paddingBottom = 3;
            halo.style.marginRight = 6; halo.style.marginTop = selected ? 0 : Lift; halo.style.marginBottom = selected ? Lift : 0;
            UiKit.Radius(halo, 10);
            halo.style.backgroundColor = selected ? new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, HaloAlpha) : Color.clear;

            var b = new Button(pick) { text = title };
            b.style.fontSize = size; b.style.color = UiKit.TabColor; b.style.flexShrink = 0;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0;
            b.style.backgroundColor = selected ? UiKit.PanelLight : Color.clear;
            UiKit.Pad(b, 6, 18); UiKit.Radius(b, 7);
            Color rest = selected ? UiKit.Gold : Color.clear;
            UiKit.Borders(b, 2, rest);
            if (!selected)
            {
                b.RegisterCallback<PointerEnterEvent>(e => UiKit.Borders(b, 2, UiKit.TabColor));
                b.RegisterCallback<PointerLeaveEvent>(e => UiKit.Borders(b, 2, rest));
            }
            halo.Add(b);
            return halo;
        }
    }
}
