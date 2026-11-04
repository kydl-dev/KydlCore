using System;
using System.Collections.Generic;

namespace KydlCore.Features.Hub.Contracts;

/// <summary>
/// T-098 (user 2026-10-31: "mods inform KydlCore about what they want displayed, and Core displays it based on its settings"): what a mod wants a hub tab to SHOW, as plain data.
/// A mod builds a page (sections of rows) and hands it to <see cref="KyHub.AddPage"/>; KydlCore builds the elements with its own theme and its own text size. A page has no
/// font, colour or size at all - the mod cannot set one. The page builder is called again every few seconds; a page whose <see cref="State"/> text equals the one on screen
/// is NOT rebuilt (no flicker, an open drop-down stays open), a different State replaces the content.
/// </summary>
public sealed class KyPage
{
    /// <summary>A text that changes exactly when something SHOWN changes (e.g. the values joined). Empty = rebuilt on every refresh.</summary>
    public string State = "";

    /// <summary>The sections, top to bottom.</summary>
    public List<KySection> Sections { get; private set; }

    /// <summary>A new, empty page.</summary>
    public KyPage() { Sections = new List<KySection>(); }

    /// <summary>Adds a section with a heading (empty title = no heading) and returns it to add rows to.</summary>
    public KySection Section(string title)
    {
        var s = new KySection(title);
        Sections.Add(s);
        return s;
    }
}

/// <summary>A block of rows under one heading.</summary>
public sealed class KySection
{
    /// <summary>The heading ("" = none).</summary>
    public string Title { get; private set; }
    /// <summary>The rows, top to bottom.</summary>
    public List<KyRow> Rows { get; private set; }

    /// <summary>Made by <see cref="KyPage.Section"/>.</summary>
    internal KySection(string title) { Title = title ?? ""; Rows = new List<KyRow>(); }

    /// <summary>A label and its value; where the value changes with quality, maxValue is shown in a second column.</summary>
    public KySection Value(string label, string value, string maxValue = "")
    {
        Rows.Add(new KyValueRow(label, value, maxValue));
        return this;
    }

    /// <summary>A small header over the value columns of the rows below (e.g. "Quality 1" | "Quality 4"). Use it before rows that have a maxValue.</summary>
    public KySection Columns(string value, string maxValue)
    {
        Rows.Add(new KyColumnsRow(value, maxValue));
        return this;
    }

    /// <summary>
    /// A element the mod brings itself - meant for a 3D viewport (a model view cannot be described as rows). KydlCore adds it, applies its text size to everything in it in the
    /// same call, and builds it again only when the page State changes (so the view keeps its rotation while the State stays). Not for text: describe text with the rows.
    /// </summary>
    public KySection Hosted(Func<UnityEngine.UIElements.VisualElement> build)
    {
        Rows.Add(new KyHostedRow(build));
        return this;
    }

    /// <summary>A line of explaining text.</summary>
    public KySection Note(string text)
    {
        Rows.Add(new KyNoteRow(text));
        return this;
    }

    /// <summary>A label and a drop-down of choices; `chosen` gets the id of the picked one (the page is built again right after).</summary>
    public KySection Choice(string label, IList<KyChoice> choices, string selected, Action<string> chosen)
    {
        Rows.Add(new KyChoiceRow(label, choices, selected, chosen));
        return this;
    }

    /// <summary>A table: a header line and one line per row, each cell a text, a button or a drop-down (see <see cref="KyCell"/>). Texts may change between builds without the State changing (they are updated in place); the rows and cell kinds may not.</summary>
    public KySection Table(IList<string> headers, IList<IList<KyCell>> rows)
    {
        Rows.Add(new KyTableRow(headers, rows));
        return this;
    }

    /// <summary>A switch with a label; `changed` gets the new state. The page is NOT rebuilt by the change (the switch shows it); put what the switch changes into the page State.</summary>
    public KySection Toggle(string label, bool on, Action<bool> changed)
    {
        Rows.Add(new KyToggleRow(label, on, changed));
        return this;
    }

    /// <summary>A slider with a label; `format` is the .NET number format of the shown value (e.g. "0.0"). `changed` is called while it is dragged; the page is NOT rebuilt by it.</summary>
    public KySection Slider(string label, float min, float max, float value, string format, Action<float> changed)
    {
        Rows.Add(new KySliderRow(label, min, max, value, format, changed));
        return this;
    }

    /// <summary>A text box with a label; `changed` gets the new text. The page is NOT rebuilt by the change.</summary>
    public KySection TextBox(string label, string value, int maxLength, Action<string> changed)
    {
        Rows.Add(new KyTextRow(label, value, maxLength, changed));
        return this;
    }

    /// <summary>A list of text lines (one bullet each); `title` is a small caption above it ("" = none).</summary>
    public KySection List(string title, IList<string> items)
    {
        Rows.Add(new KyListRow(title, items));
        return this;
    }

    /// <summary>A button with a label.</summary>
    public KySection Button(string label, Action clicked)
    {
        Rows.Add(new KyButtonRow(label, clicked));
        return this;
    }
}

/// <summary>One row of a <see cref="KySection"/>.</summary>
public abstract class KyRow
{
    /// <summary>The label (the text for a note).</summary>
    public string Label { get; private set; }

    internal KyRow(string label) { Label = label ?? ""; }
}

/// <summary>Label and value (made by <see cref="KySection.Value"/>).</summary>
public sealed class KyValueRow : KyRow
{
    /// <summary>The value.</summary>
    public string Value { get; private set; }
    /// <summary>The value at max quality ("" = the value does not change with quality).</summary>
    public string MaxValue { get; private set; }
    internal KyValueRow(string label, string value, string maxValue) : base(label) { Value = value ?? ""; MaxValue = maxValue ?? ""; }
}

/// <summary>Explaining text (made by <see cref="KySection.Note"/>).</summary>
public sealed class KyNoteRow : KyRow
{
    internal KyNoteRow(string text) : base(text) { }
}

/// <summary>One entry of a drop-down.</summary>
public sealed class KyChoice
{
    /// <summary>What the mod identifies it by.</summary>
    public string Id { get; private set; }
    /// <summary>What the player reads.</summary>
    public string Text { get; private set; }
    /// <summary>Makes a choice.</summary>
    public KyChoice(string id, string text) { Id = id ?? ""; Text = text ?? ""; }
}

/// <summary>A drop-down (made by <see cref="KySection.Choice"/>).</summary>
public sealed class KyChoiceRow : KyRow
{
    /// <summary>The entries.</summary>
    public IList<KyChoice> Choices { get; private set; }
    /// <summary>The id shown as picked.</summary>
    public string Selected { get; private set; }
    /// <summary>Called with the id of the picked entry.</summary>
    public Action<string> Chosen { get; private set; }
    internal KyChoiceRow(string label, IList<KyChoice> choices, string selected, Action<string> chosen) : base(label)
    {
        Choices = choices ?? new List<KyChoice>(); Selected = selected ?? ""; Chosen = chosen;
    }
}

/// <summary>A button (made by <see cref="KySection.Button"/>).</summary>
public sealed class KyButtonRow : KyRow
{
    /// <summary>Called when it is pressed.</summary>
    public Action Clicked { get; private set; }
    internal KyButtonRow(string label, Action clicked) : base(label) { Clicked = clicked; }
}

/// <summary>One cell of a table row. Made with <see cref="Text"/>, <see cref="Button"/> or <see cref="Drop"/>.</summary>
public sealed class KyCell
{
    internal enum Kinds { Text, Button, Drop }
    internal Kinds Kind;
    /// <summary>True when the text may hold rich-text tags (colours, emoji) - e.g. a player-styled name.</summary>
    public bool IsRich { get; private set; }
    /// <summary>The text (or the button label).</summary>
    public string Text { get; private set; }
    /// <summary>Called when the button is pressed (after the confirmation, if there is one).</summary>
    public Action Clicked { get; private set; }
    /// <summary>The question asked in the cell before a button acts ("" = acts at once).</summary>
    public string ConfirmText { get; private set; }
    /// <summary>The label of the confirming button.</summary>
    public string YesText { get; private set; }
    /// <summary>The label of the button that takes it back.</summary>
    public string NoText { get; private set; }
    /// <summary>The drop-down entries.</summary>
    public IList<KyChoice> Choices { get; private set; }
    /// <summary>The id shown as picked in a drop-down.</summary>
    public string Selected { get; private set; }
    /// <summary>Called with the picked id.</summary>
    public Action<string> Chosen { get; private set; }

    KyCell() { Text = ""; ConfirmText = ""; YesText = ""; NoText = ""; Selected = ""; }

    /// <summary>A text cell.</summary>
    public static KyCell Label(string text) { return new KyCell { Kind = Kinds.Text, Text = text ?? "" }; }

    /// <summary>A text cell that shows rich-text tags (a player-styled name).</summary>
    public static KyCell Rich(string text) { return new KyCell { Kind = Kinds.Text, Text = text ?? "", IsRich = true }; }

    /// <summary>A button cell.</summary>
    public static KyCell Button(string label, Action clicked) { return new KyCell { Kind = Kinds.Button, Text = label ?? "", Clicked = clicked }; }

    /// <summary>A drop-down cell.</summary>
    public static KyCell Drop(IList<KyChoice> choices, string selected, Action<string> chosen)
    {
        return new KyCell { Kind = Kinds.Drop, Choices = choices ?? new List<KyChoice>(), Selected = selected ?? "", Chosen = chosen };
    }

    /// <summary>Makes this button ask `question` with Yes / No buttons in its place before it acts. Returns the cell.</summary>
    public KyCell Confirm(string question, string yes, string no)
    {
        ConfirmText = question ?? ""; YesText = yes ?? ""; NoText = no ?? "";
        return this;
    }
}

/// <summary>A table (made by <see cref="KySection.Table"/>).</summary>
public sealed class KyTableRow : KyRow
{
    /// <summary>The header texts.</summary>
    public IList<string> Headers { get; private set; }
    /// <summary>The rows of cells.</summary>
    public IList<IList<KyCell>> Cells { get; private set; }
    internal KyTableRow(IList<string> headers, IList<IList<KyCell>> cells) : base("")
    {
        Headers = headers ?? new List<string>(); Cells = cells ?? new List<IList<KyCell>>();
    }
}

/// <summary>The header of the value columns (made by <see cref="KySection.Columns"/>).</summary>
public sealed class KyColumnsRow : KyRow
{
    /// <summary>The header over the value column.</summary>
    public string Value { get; private set; }
    /// <summary>The header over the max-quality column.</summary>
    public string MaxValue { get; private set; }
    internal KyColumnsRow(string value, string maxValue) : base("") { Value = value ?? ""; MaxValue = maxValue ?? ""; }
}

/// <summary>An element the mod builds (made by <see cref="KySection.Hosted"/>).</summary>
public sealed class KyHostedRow : KyRow
{
    /// <summary>Builds the element.</summary>
    public Func<UnityEngine.UIElements.VisualElement> Build { get; private set; }
    internal KyHostedRow(Func<UnityEngine.UIElements.VisualElement> build) : base("") { Build = build; }
}

/// <summary>A switch (made by <see cref="KySection.Toggle"/>).</summary>
public sealed class KyToggleRow : KyRow
{
    /// <summary>Shown as on.</summary>
    public bool On { get; private set; }
    /// <summary>Called with the new state.</summary>
    public Action<bool> Changed { get; private set; }
    internal KyToggleRow(string label, bool on, Action<bool> changed) : base(label) { On = on; Changed = changed; }
}

/// <summary>A slider (made by <see cref="KySection.Slider"/>).</summary>
public sealed class KySliderRow : KyRow
{
    /// <summary>Lowest value.</summary>
    public float Min { get; private set; }
    /// <summary>Highest value.</summary>
    public float Max { get; private set; }
    /// <summary>The value shown.</summary>
    public float Value { get; private set; }
    /// <summary>The .NET number format of the shown value.</summary>
    public string Format { get; private set; }
    /// <summary>Called with the new value.</summary>
    public Action<float> Changed { get; private set; }
    internal KySliderRow(string label, float min, float max, float value, string format, Action<float> changed) : base(label)
    {
        Min = min; Max = max < min ? min : max; Value = value; Format = string.IsNullOrEmpty(format) ? "0.##" : format; Changed = changed;
    }
}

/// <summary>A text box (made by <see cref="KySection.TextBox"/>).</summary>
public sealed class KyTextRow : KyRow
{
    /// <summary>The text shown.</summary>
    public string Value { get; private set; }
    /// <summary>Longest text the box takes.</summary>
    public int MaxLength { get; private set; }
    /// <summary>Called with the new text.</summary>
    public Action<string> Changed { get; private set; }
    internal KyTextRow(string label, string value, int maxLength, Action<string> changed) : base(label) { Value = value ?? ""; MaxLength = maxLength < 1 ? 64 : maxLength; Changed = changed; }
}

/// <summary>A list of lines (made by <see cref="KySection.List"/>).</summary>
public sealed class KyListRow : KyRow
{
    /// <summary>The lines.</summary>
    public IList<string> Items { get; private set; }
    internal KyListRow(string title, IList<string> items) : base(title) { Items = items ?? new List<string>(); }
}
