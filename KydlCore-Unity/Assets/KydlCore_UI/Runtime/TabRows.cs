using System;
using System.Collections.Generic;
using System.Linq;
using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore.Ui
{
    /// <summary>
    /// One wrapping row of hub tabs (PLAN T-035, D-027): the tabs wrap onto lines; up to 2 lines are shown. With more, a thin scroll bar and two small
    /// arrows (the number of the top line between them) appear on the right and the mouse wheel moves one line; a selected tab on a hidden line is
    /// brought into view. The lines and positions are decided by the pure <see cref="TabPaging"/>; this class measures the tabs (UI Toolkit lays them out
    /// first) and moves the content. Compiled into the mod and shown by the Unity preview.
    /// </summary>
    internal sealed class TabRows
    {
        const float RowTolerance = 6f;        // tabs on one line differ by the lift of the selected one (3 px)
        const float BarW = 4f, PagerW = 28f, Gap = 6f, Square = 26f;

        readonly VisualElement _view = new VisualElement { name = "kydl-tabrows-view" };
        readonly VisualElement _content = new VisualElement { name = "kydl-tabrows-content" };
        readonly VisualElement _bar = new VisualElement { name = "kydl-tabrows-bar" };
        readonly VisualElement _thumb = new VisualElement { name = "kydl-tabrows-thumb" };
        readonly VisualElement _pager = new VisualElement { name = "kydl-tabrows-pager" };
        readonly Label _number;
        readonly List<VisualElement> _tabs = new List<VisualElement>();
        int _selected = -1, _top, _rows;
        bool _revealPending;

        /// <summary>The element to put into the hub window.</summary>
        internal VisualElement Root { get; private set; }

        internal TabRows(string name)
        {
            Root = new VisualElement { name = name };
            Root.style.flexDirection = FlexDirection.Row; Root.style.flexShrink = 0; Root.style.alignItems = Align.FlexStart;

            _view.style.flexGrow = 1; _view.style.flexShrink = 1; _view.style.overflow = Overflow.Hidden;
            _content.style.flexDirection = FlexDirection.Row; _content.style.flexWrap = Wrap.Wrap; _content.style.alignItems = Align.FlexStart;
            _content.style.flexShrink = 0;      // else the content shrinks to the (smaller) view on the last page, its height is wrong and the page is blank (D-062)
            _view.Add(_content);

            _bar.style.width = BarW; _bar.style.marginLeft = Gap; _bar.style.marginTop = 2; _bar.style.display = DisplayStyle.None; _bar.style.flexShrink = 0;
            UiKit.Radius(_bar, 2);
            _bar.style.backgroundColor = UiKit.Track;
            _thumb.style.position = Position.Absolute; _thumb.style.left = 0; _thumb.style.right = 0;
            UiKit.Radius(_thumb, 2);
            _thumb.style.backgroundColor = UiKit.GoldDim;
            _bar.Add(_thumb);

            _pager.style.width = PagerW; _pager.style.marginLeft = Gap; _pager.style.alignItems = Align.Center; _pager.style.display = DisplayStyle.None; _pager.style.flexShrink = 0;
            _number = UiKit.Label("1", UiKit.SmallSize, UiKit.TextDim);
            _number.style.unityTextAlign = TextAnchor.MiddleCenter;
            _pager.Add(Arrow(-1));
            _pager.Add(_number);
            _pager.Add(Arrow(1));

            Root.Add(_view); Root.Add(_bar); Root.Add(_pager);
            _content.RegisterCallback<GeometryChangedEvent>(e => Reflow());
            Root.RegisterCallback<WheelEvent>(OnWheel);
        }

        /// <summary>The line shown first (0-based).</summary>
        internal int Top { get { return _top; } }

        /// <summary>How many lines the tabs fill (known after the first layout).</summary>
        internal int Rows { get { return _rows; } }

        /// <summary>Removes every tab; the next Add starts a new set.</summary>
        internal void Clear()
        {
            _content.Clear();
            _tabs.Clear();
            _selected = -1;
        }

        /// <summary>Adds a tab at the end; the selected one is brought into view once the lines are known.</summary>
        internal void Add(VisualElement tab, bool selected)
        {
            if (selected) { _selected = _tabs.Count; _revealPending = true; }
            _tabs.Add(tab);
            _content.Add(tab);
        }

        // Called by UI Toolkit when the tabs were laid out (and again when they change).
        void Reflow()
        {
            var tops = _tabs.Select(t => t.layout.y).Where(y => !float.IsNaN(y)).ToList();
            if (tops.Count != _tabs.Count || tops.Count == 0) { SetRows(0); return; }
            float[] rowTops = TabPaging.RowTops(tops, RowTolerance);
            SetRows(rowTops.Length);
            if (_revealPending && _selected >= 0)
            {
                _revealPending = false;
                _top = TabPaging.Reveal(TabPaging.RowOf(tops, RowTolerance, _selected), _top, _rows);
            }
            _top = TabPaging.Clamp(_top, _rows);
            bool paging = TabPaging.Needed(_rows);
            float height = paging ? TabPaging.ViewHeight(rowTops, _content.layout.height, _top) : -1f;
            _content.style.translate = new Translate(0f, -TabPaging.Offset(rowTops, _top));
            ShowPaging(paging, height);
        }

        void SetRows(int rows)
        {
            _rows = rows;
            if (!TabPaging.Needed(rows)) ShowPaging(false, -1f);
        }

        void ShowPaging(bool on, float viewHeight)
        {
            _view.style.height = on ? new StyleLength(viewHeight) : new StyleLength(StyleKeyword.Auto);
            _bar.style.display = _pager.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) { _top = 0; return; }
            _bar.style.height = viewHeight - 4f;
            _thumb.style.top = Length.Percent(100f * TabPaging.ThumbStart(_rows, _top));
            _thumb.style.height = Length.Percent(100f * TabPaging.ThumbLength(_rows));
            _number.text = (_top + 1).ToString();
        }

        void Move(int delta)
        {
            int next = TabPaging.Step(_top, delta, _rows);
            if (next == _top) return;
            _top = next;
            Reflow();
            UiLog.Verbose("TabRows: top line " + _top + " of " + _rows);
        }

        void OnWheel(WheelEvent e)
        {
            if (!TabPaging.Needed(_rows)) return;
            Move(e.delta.y > 0f ? 1 : -1);
            e.StopPropagation();
        }

        static void DrawTriangle(MeshGenerationContext ctx, Rect r, bool up)
        {
            if (r.width < 1f || r.height < 1f) return;
            float w = 12f, h = 8f, cx = r.width * 0.5f, cy = r.height * 0.5f;
            var p = ctx.painter2D;
            p.fillColor = UiKit.Gold;
            p.BeginPath();
            if (up) { p.MoveTo(new Vector2(cx - w / 2f, cy + h / 2f)); p.LineTo(new Vector2(cx + w / 2f, cy + h / 2f)); p.LineTo(new Vector2(cx, cy - h / 2f)); }
            else { p.MoveTo(new Vector2(cx - w / 2f, cy - h / 2f)); p.LineTo(new Vector2(cx + w / 2f, cy - h / 2f)); p.LineTo(new Vector2(cx, cy + h / 2f)); }
            p.ClosePath();
            p.Fill();
        }

        VisualElement Arrow(int delta)
        {
            var b = new Button(() => Move(delta));
            b.style.fontSize = UiKit.SmallSize; b.style.color = UiKit.Gold; b.style.backgroundColor = Color.clear; b.style.flexShrink = 0;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0;
            b.style.width = Square; b.style.height = Square;                  // a square, the glyph exactly in its middle
            b.style.justifyContent = Justify.Center; b.style.alignItems = Align.Center;
            var tri = new VisualElement { pickingMode = PickingMode.Ignore };           // a drawn triangle: exactly in the middle, whatever the font does
            tri.style.position = Position.Absolute; tri.style.left = 0; tri.style.right = 0; tri.style.top = 0; tri.style.bottom = 0;
            tri.generateVisualContent += ctx => DrawTriangle(ctx, tri.contentRect, delta < 0);
            b.Add(tri);
            UiKit.Pad(b, 0, 0); UiKit.Radius(b, 4); UiKit.Borders(b, 1, UiKit.Border);
            b.RegisterCallback<PointerEnterEvent>(e => { UiKit.Borders(b, 1, UiKit.TabColor); b.style.backgroundColor = UiKit.PanelHover; });
            b.RegisterCallback<PointerLeaveEvent>(e => { UiKit.Borders(b, 1, UiKit.Border); b.style.backgroundColor = Color.clear; });
            return b;
        }
    }
}
