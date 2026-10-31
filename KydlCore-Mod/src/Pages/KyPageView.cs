using System;
using System.Collections.Generic;
using Kydl.Common;
using Kydl.Ui;
using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore
{
    /// <summary>
    /// T-098: the renderer of <see cref="KyPage"/>. Builds the elements with the kit's theme and scales the text to the hub's page size in the SAME call, before the element is
    /// returned (so no frame ever shows the unscaled size), and replaces the content only when the page's State changed.
    /// </summary>
    internal static class KyPageView
    {
        const float LabelWidth = 210f, ValueWidth = 140f;

        /// <summary>A host element showing what `build` returns, refreshed every `refreshMs`.</summary>
        internal static VisualElement Host(Func<KyPage> build, int refreshMs)
        {
            var host = new VisualElement { name = "kydl-page" };
            host.style.flexShrink = 0;
            string shown = null;
            List<Label> live = null;
            Action render = null;
            render = () =>
            {
                KyPage page;
                try { page = build(); }
                catch (Exception e) { ModLog.Warn("KyPageView: the page builder threw " + e.GetType().Name + ": " + e.Message); return; }
                if (page == null) return;
                if (!PageRules.NeedsRebuild(shown, page.State)) { UpdateTexts(live, page); return; }
                shown = page.State ?? "";
                host.Clear();
                live = new List<Label>();
                Fill(host, page, () => { shown = null; render(); }, live);
                ConfigPage.ScaleFonts(host);
            };
            render();
            var tick = host.schedule.Execute(render).Every(Math.Max(250, refreshMs));
            host.RegisterCallback<DetachFromPanelEvent>(e => tick.Pause());
            host.RegisterCallback<AttachToPanelEvent>(e => tick.Resume());
            return host;
        }

        static void Fill(VisualElement host, KyPage page, Action rebuild, List<Label> live)
        {
            foreach (var s in page.Sections)
            {
                if (s.Title.Length > 0) host.Add(UiControls.Heading(s.Title));
                foreach (var row in s.Rows)
                {
                    var v = row as KyValueRow; var n = row as KyNoteRow; var c = row as KyChoiceRow; var b = row as KyButtonRow; var t = row as KyTableRow; var k = row as KyColumnsRow; var h = row as KyHostedRow;
                    var tg = row as KyToggleRow; var sl = row as KySliderRow; var tx = row as KyTextRow; var li = row as KyListRow;
                    if (tg != null) host.Add(UiControls.Toggle(tg.Label, tg.On, on => { if (tg.Changed != null) tg.Changed(on); }));
                    else if (sl != null) host.Add(UiControls.Row(sl.Label, UiControls.Slider(sl.Min, sl.Max, sl.Value, sl.Format, v => { if (sl.Changed != null) sl.Changed(v); })));
                    else if (tx != null) host.Add(UiControls.Row(tx.Label, UiControls.TextField(tx.Value, tx.MaxLength, v => { if (tx.Changed != null) tx.Changed(v); })));
                    else if (li != null) AddList(host, li);
                    else if (h != null) { VisualElement el = null; try { el = h.Build != null ? h.Build() : null; } catch (Exception e) { ModLog.Warn("KyPageView: a hosted element threw " + e.GetType().Name + ": " + e.Message); } if (el != null) host.Add(el); }
                    else if (k != null) host.Add(ColumnsLine(k));
                    else if (t != null) AddTable(host, t, rebuild, live);
                    else if (v != null) host.Add(ValueLine(v));
                    else if (n != null) host.Add(UiControls.Note(n.Label));
                    else if (c != null) host.Add(ChoiceLine(c, rebuild));
                    else if (b != null) host.Add(UiControls.Button(b.Label, () => { if (b.Clicked != null) b.Clicked(); rebuild(); }));
                }
            }
        }

        /// <summary>The text cells of a page, in the order Fill adds them (the order UpdateTexts walks).</summary>
        static IEnumerable<string> TextsOf(KyPage page)
        {
            foreach (var s in page.Sections)
                foreach (var row in s.Rows)
                {
                    var t = row as KyTableRow;
                    if (t == null) continue;
                    foreach (var h in t.Headers) yield return h;
                    foreach (var r in t.Cells) foreach (var c in r) if (c.Kind == KyCell.Kinds.Text) yield return c.Text;
                }
        }

        /// <summary>Same State: only the texts may differ; set the ones that did (no rebuild, so nothing flickers and an open confirmation stays).</summary>
        static void UpdateTexts(List<Label> live, KyPage page)
        {
            if (live == null) return;
            int i = 0;
            foreach (var text in TextsOf(page))
            {
                if (i >= live.Count) return;
                if (live[i].text != text) live[i].text = text;
                i++;
            }
        }

        static void AddTable(VisualElement host, KyTableRow table, Action rebuild, List<Label> live)
        {
            if (table.Headers.Count > 0)
            {
                var head = Line();
                for (int i = 0; i < table.Headers.Count; i++) head.Add(Track(Cell(table.Headers[i], ColWidth(i), UiKit.TextDim), live));
                host.Add(head);
            }
            foreach (var cells in table.Cells)
            {
                var line = Line();
                for (int i = 0; i < cells.Count; i++) line.Add(CellElement(cells[i], i, rebuild, live));
                host.Add(line);
            }
        }

        static VisualElement Line()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
            r.style.marginTop = 3; r.style.marginBottom = 3;
            return r;
        }

        static float ColWidth(int column) { return column == 0 ? 170f : 100f; }

        static Label Track(Label l, List<Label> live) { live.Add(l); return l; }

        static VisualElement CellElement(KyCell c, int column, Action rebuild, List<Label> live)
        {
            if (c.Kind == KyCell.Kinds.Text)
            {
                var label = Cell(c.Text, ColWidth(column), UiKit.Text);
                if (c.IsRich) label.enableRichText = true;
                return Track(label, live);
            }
            if (c.Kind == KyCell.Kinds.Drop)
            {
                var ids = new List<string>(); var text = new Dictionary<string, string>();
                foreach (var ch in c.Choices) { ids.Add(ch.Id); text[ch.Id] = ch.Text; }
                string selected = ids.Contains(c.Selected) ? c.Selected : (ids.Count > 0 ? ids[0] : "");
                var drop = UiControls.Dropdown(ids, selected, id => { string t; return text.TryGetValue(id, out t) ? t : id; }, id => { if (c.Chosen != null) c.Chosen(id); rebuild(); });
                drop.style.width = 180f; drop.style.flexShrink = 0; drop.style.marginRight = 4;
                return drop;
            }
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row; box.style.alignItems = Align.Center; box.style.flexShrink = 0;
            Action normal = null;
            Action act = () => { if (c.Clicked != null) c.Clicked(); rebuild(); };
            normal = () =>
            {
                box.Clear();
                box.Add(Sized(UiControls.Button(c.Text, c.ConfirmText.Length == 0 ? act : (Action)(() =>
                {
                    box.Clear();
                    box.Add(Cell(c.ConfirmText, -1f, UiKit.Text));
                    box.Add(Sized(UiControls.Button(c.YesText, act, true)));
                    box.Add(Sized(UiControls.Button(c.NoText, normal)));
                    ConfigPage.ScaleFonts(box);
                }))));
                ConfigPage.ScaleFonts(box);
            };
            normal();
            return box;
        }

        static Button Sized(Button b) { b.style.width = 90f; b.style.flexShrink = 0; b.style.marginRight = 4; return b; }

        static void AddList(VisualElement host, KyListRow list)
        {
            if (list.Label.Length > 0) host.Add(Cell(list.Label, -1f, UiKit.TextDim));
            foreach (var item in list.Items) host.Add(Cell("\u2022 " + item, -1f, UiKit.Text));
        }

        static VisualElement ColumnsLine(KyColumnsRow row)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.FlexStart; r.style.flexShrink = 0;
            r.style.marginTop = 2; r.style.marginBottom = 2;
            r.Add(Cell("", LabelWidth, UiKit.TextDim));
            r.Add(Cell(row.Value, ValueWidth, UiKit.TextDim));
            r.Add(Cell(row.MaxValue, ValueWidth, UiKit.TextDim));
            return r;
        }

        static VisualElement ValueLine(KyValueRow row)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.FlexStart; r.style.flexShrink = 0;
            r.style.marginTop = 2; r.style.marginBottom = 2;
            bool max = row.MaxValue.Length > 0;
            r.Add(Cell(row.Label, LabelWidth, UiKit.TextDim));
            r.Add(Cell(row.Value, max ? ValueWidth : -1f, UiKit.Text));
            if (max) r.Add(Cell(row.MaxValue, ValueWidth, UiKit.Gold));
            return r;
        }

        static Label Cell(string text, float width, Color color)
        {
            var l = UiKit.Label(text, UiKit.FontSize, color);
            l.style.whiteSpace = WhiteSpace.Normal;
            if (width >= 0f) { l.style.width = width; l.style.flexShrink = 0; }
            else { l.style.flexGrow = 1; l.style.flexShrink = 1; }
            return l;
        }

        static VisualElement ChoiceLine(KyChoiceRow row, Action rebuild)
        {
            var ids = new System.Collections.Generic.List<string>();
            var text = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var ch in row.Choices) { ids.Add(ch.Id); text[ch.Id] = ch.Text; }
            string selected = ids.Contains(row.Selected) ? row.Selected : (ids.Count > 0 ? ids[0] : "");
            var drop = UiControls.Dropdown(ids, selected, id => { string t; return text.TryGetValue(id, out t) ? t : id; }, id => { if (row.Chosen != null) row.Chosen(id); rebuild(); });
            return UiControls.Row(row.Label, drop);
        }
    }
}
