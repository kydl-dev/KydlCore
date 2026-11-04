using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using KydlCore.Common;
using KydlCore.Features.Hub.Infrastructure;
using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore.Presentation.Hub.Pages;

/// <summary>
/// The page of the auto "Settings" tab (PLAN T-033, D-023): one control for EVERY config entry of a mod, grouped by section. A change in the page
/// is written through <c>ConfigEntryBase.BoxedValue</c> (so the mod's own SettingChanged handlers apply it in game at once); a change made in the
/// .cfg file / by code comes in through <c>ConfigFile.SettingChanged</c> and redraws the page (hot reload, both ways, PROJECT_RULES 17). Which
/// control an entry gets, number reading and the Browsable / IsAdminOnly tags are the pure <see cref="ConfigControlMap"/>. Drawing only: tested in
/// game / in the preview, not by unit tests.
/// </summary>
internal sealed class ConfigPage : IDisposable
{
    const long PollMs = 250;                  // the config event can come from the file watcher thread: the page redraws on the UI thread
    const int TextMax = 256;
    const float FontScale = HubView.PageFontScale;  // every text of the page is this part of the kit's size (user 2026-10-30: 65%, D-061)
    const float SliderGapPercent = 6f;
    const float LabelWidth = 230f;            // the setting-name column (before the scale does not apply to widths)

    readonly ConfigFile _file;
    readonly string[] _sections;              // null / empty = every section
    readonly VisualElement _root = new VisualElement { name = "kydl-config-page" };
    volatile bool _dirty;
    bool _writing;
    IVisualElementScheduledItem _poll;

    internal ConfigPage(ConfigFile file, string[] sections = null)
    {
        _file = file;
        _sections = sections != null && sections.Length > 0 ? sections : null;
        _root.style.flexShrink = 0;
        _file.SettingChanged += OnSettingChanged;
        Fill();
        _poll = _root.schedule.Execute(Poll).Every(PollMs);
        ModLog.Verbose("ConfigPage: built for " + (file == null ? "(no file)" : file.ConfigFilePath));
    }

    /// <summary>The page; the hub puts it under the tab row.</summary>
    internal VisualElement Root { get { return _root; } }

    /// <summary>Stops listening to the config file (call when the page is thrown away).</summary>
    public void Dispose()
    {
        _file.SettingChanged -= OnSettingChanged;
        KeyCapture.Cancel("page closed");
        if (_poll != null) _poll.Pause();
        ModLog.Verbose("ConfigPage: disposed");
    }

    void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        if (_writing) return;                 // our own write: the control already shows the value
        _dirty = true;
    }

    // ServerSync flips the ReadOnly tag of its entries when this player's admin state arrives or changes, without a SettingChanged event:
    // the page notices by comparing which entries are locked now with what it drew.
    string _lockSignature = "";

    string LockSignature()
    {
        var sb = new System.Text.StringBuilder();
        foreach (ConfigDefinition def in _file.Keys)
            if (InSections(def.Section))
            {
                IEnumerable<object> tags = Tags(_file[def]);
                sb.Append(ConfigControlMap.IsReadOnly(tags) ? '1' : '0').Append(ConfigControlMap.IsHidden(tags) ? 'h' : 's');   // a tag may change a flag while the game runs
            }
        return sb.ToString();
    }

    void Poll()
    {
        string sig = LockSignature();
        if (sig != _lockSignature) _dirty = true;
        if (!_dirty) return;
        _dirty = false;
        ModLog.Verbose("ConfigPage: a setting changed outside the page, redrawing");
        Fill();
    }

    // --- drawing -------------------------------------------------------------------------------------------------------
    void Fill()
    {
        KeyCapture.Cancel("page redrawn");
        _lockSignature = LockSignature();
        _root.Clear();
        var shown = new List<ConfigEntryBase>();
        foreach (ConfigDefinition def in _file.Keys)
        {
            ConfigEntryBase entry = _file[def];
            if (InSections(def.Section) && !ConfigControlMap.IsHidden(Tags(entry))) shown.Add(entry);
        }
        foreach (var section in shown.GroupBy(e => e.Definition.Section))        // GroupBy keeps the order of first appearance
        {
            _root.Add(UiControls.Heading(section.Key));
            foreach (ConfigEntryBase entry in section) _root.Add(Control(entry));
        }
        if (shown.Count == 0) _root.Add(UiControls.Note("This mod has no settings."));
        ScaleFonts(_root);
        ModLog.Verbose("ConfigPage: " + shown.Count + " setting(s) shown");
    }

    bool InSections(string section)
    {
        return _sections == null || Array.IndexOf(_sections, section) >= 0;
    }

    static IEnumerable<object> Tags(ConfigEntryBase entry)
    {
        return entry.Description == null ? null : entry.Description.Tags;
    }

    VisualElement Control(ConfigEntryBase entry)
    {
        string key = entry.Definition.Key;
        var box = new VisualElement();
        box.style.flexShrink = 0;
        IEnumerable<object> tags = Tags(entry);
        bool serverLocked = ConfigControlMap.IsReadOnly(tags);              // ServerSync: this player may not change the server's value
        bool locked = serverLocked || (ConfigControlMap.IsAdminOnly(tags) && !MayEditAdminOnly());
        VisualElement control = locked ? LockedValue(entry) : Make(entry, key);
        if (control.name != "kydl-toggle") Compact(control);
        box.Add(control.name == "kydl-toggle" ? control : Row(key, control));
        if (locked) box.Add(UiControls.Note(serverLocked ? "Set by the server: only an admin can change this." : "Admin only: only the host / an admin can change this."));
        string text = entry.Description == null ? null : entry.Description.Description;
        if (!string.IsNullOrEmpty(text)) box.Add(UiControls.Note(text));
        ModLog.Verbose("ConfigPage: control for " + entry.Definition.Section + " / " + key + " (" + entry.SettingType.Name + ")" + (locked ? " locked" : ""));
        return box;
    }

    // One line: the name on the left, the control right of it at the size of its content (a drop-down is only as wide as its text); only a
    // slider / colour editor stretches. The name does not wrap.
    static VisualElement Row(string label, VisualElement control)
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
        r.style.marginTop = 4; r.style.marginBottom = 4;
        var l = UiKit.Label(label, UiKit.NameSize, UiKit.NameColor);
        // fixed column; a name longer than the column is cut with "..." instead of running under the slider (user 2026-11-04)
        l.style.width = LabelWidth; l.style.minWidth = LabelWidth; l.style.flexShrink = 0; l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.overflow = Overflow.Hidden; l.style.textOverflow = TextOverflow.Ellipsis; l.style.marginRight = 8;
        l.tooltip = label;
        r.Add(l);
        bool stretch = control.name == "kydl-slider" || control.name == "kydl-color";
        bool textBox = control.name == "kydl-text";
        control.style.flexGrow = stretch ? 1f : 0f;
        control.style.flexShrink = stretch || textBox ? 1f : 0f;      // a text box gives way when the panel is narrow; its text is clipped inside
        if (stretch || textBox) control.style.minWidth = 60;
        if (!stretch) { control.style.alignSelf = Align.FlexStart; control.style.marginLeft = StyleKeyword.Auto; }   // value fields sit at the right edge (D-062)
        else control.style.marginLeft = Length.Percent(SliderGapPercent);      // user 2026-11-04: ~10 % shorter sliders and a gap to the name (percent of the row ~ 10 % of the slider)
        r.Add(control);
        return r;
    }

    // The text in the value fields (number / text box, drop-down, colour editor) has the size of the drop-down text (the "F9" of PoolMenuKey, the
    // kit's FontSize), with a small margin inside the box (user 2026-10-30, D-063). The page scale is applied afterwards.
    static void Compact(VisualElement control)
    {
        control.Query<VisualElement>().ForEach(e =>
        {
            if (e.style.fontSize.keyword == StyleKeyword.Undefined && e.style.fontSize.value.value > 0f) e.style.fontSize = UiKit.FontSize;
        });
        control.Query<TextField>().ForEach(f =>
        {
            VisualElement input = f.Q(className: TextField.inputUssClassName) ?? f;
            UiKit.Pad(input, 1, 5);
            input.style.fontSize = UiKit.FontSize;
            f.style.overflow = Overflow.Hidden;                                 // long text (a webhook URL) is clipped by the box, not drawn past its border
            input.style.overflow = Overflow.Hidden;
            input.style.minWidth = 0;
            f.Query<TextElement>().ForEach(t => { t.style.overflow = Overflow.Hidden; t.style.flexShrink = 1; t.style.minWidth = 0; });
        });
        control.Query<Button>().ForEach(b => UiKit.Pad(b, 1, 5));
    }

    // Makes every text of the page smaller by FontScale (the kit's controls set their sizes inline, so the inline value is scaled).
    // D-078 (user 2026-10-30 23:39: the SteveTheTroll Trolls page "autoresizes to a bigger font-size"): a page that builds more of itself later (a new troll chosen in
    // its drop-down) added elements with the kit's full inline sizes after the one-off scaling. Every element is scaled ONCE (marked with ScaledClass), so the scaling
    // can be repeated - KyHub.KeepScaled does that while the page is shown.
    internal const string ScaledClass = "kydl-font-scaled";

    internal static void ScaleFonts(VisualElement root)
    {
        root.Query<VisualElement>().ForEach(e =>
        {
            if (e.ClassListContains(ScaledClass)) return;
            StyleLength fs = e.style.fontSize;
            if (fs.keyword == StyleKeyword.Undefined && fs.value.value > 0f) { e.style.fontSize = fs.value.value * FontScale; e.AddToClassList(ScaledClass); }
        });
    }

    // A client of someone else's server may not change an admin-only entry (the server owns it); in single player and on the host everything is open.
    static bool MayEditAdminOnly()
    {
        return ZNet.instance == null || ZNet.instance.IsServer();
    }

    static VisualElement LockedValue(ConfigEntryBase entry)
    {
        return UiControls.Note(entry.GetSerializedValue());
    }

    VisualElement Make(ConfigEntryBase entry, string key)
    {
        Type type = entry.SettingType;
        double min, max;
        bool hasRange = Range(entry, out min, out max);
        IList<string> list = ListOf(entry);
        switch (ConfigControlMap.Classify(type, hasRange, list != null))
        {
            case ControlKind.Toggle: return Toggle(entry, key);
            case ControlKind.Slider: return Slider(entry, min, max);
            case ControlKind.Number: return Sized(UiControls.TextField(ConfigControlMap.Format(type, entry.BoxedValue), TextMax, t => Write(entry, ConfigControlMap.Parse(type, t))), 160f, "kydl-text");
            case ControlKind.Text: return Sized(UiControls.TextField((string)entry.BoxedValue, TextMax, t => Write(entry, t)), 360f, "kydl-text");
            case ControlKind.Dropdown: return Dropdown(entry, list);
            case ControlKind.Color: return ColorEditor(entry);
            case ControlKind.Key: return RebindButton(entry, false);
            case ControlKind.Shortcut: return RebindButton(entry, true);
            default: return UiControls.Note(entry.GetSerializedValue());
        }
    }

    static VisualElement Sized(VisualElement box, float width, string name)
    {
        box.name = name;
        box.style.width = width;
        return box;
    }

    VisualElement Slider(ConfigEntryBase entry, double min, double max)
    {
        Type type = entry.SettingType;
        var slider = UiControls.Slider((float)min, (float)max, Convert.ToSingle(entry.BoxedValue), ConfigControlMap.SliderFormat(type), v => Write(entry, ConfigControlMap.Clamp(type, v, min, max)));
        slider.name = "kydl-slider";
        slider.style.minHeight = 24;
        var input = slider.Q<TextField>();                                      // the number box: narrower and lower (user 2026-10-30, D-061)
        if (input != null) input.style.width = 46;
        return slider;
    }

    VisualElement Toggle(ConfigEntryBase entry, string key)
    {
        var t = UiControls.Toggle(key, (bool)entry.BoxedValue, on => Write(entry, on));
        t.name = "kydl-toggle";
        // the kit puts the switch before its text; here the text comes first and the switch sits at the right edge (user 2026-10-30, D-061)
        VisualElement sw = t[0], label = t[1];
        t.Insert(0, label);
        label.style.flexGrow = 1;
        sw.style.marginRight = 0; sw.style.marginLeft = 8;
        t.style.paddingLeft = 0; t.style.marginRight = 0;
        return t;
    }

    VisualElement Dropdown(ConfigEntryBase entry, IList<string> list)
    {
        Type type = entry.SettingType;
        IList<string> options = type.IsEnum ? Enum.GetNames(type) : list;
        string current = type.IsEnum ? Enum.GetName(type, entry.BoxedValue) : (string)entry.BoxedValue;
        return UiControls.Dropdown(options, current ?? options.FirstOrDefault() ?? "", s => s,
            chosen => Write(entry, type.IsEnum ? Enum.Parse(type, chosen) : chosen));
    }

    // Press-to-rebind (user 2026-10-30, D-075): the button shows the key; a click makes it wait ("Press a key..."), the next key pressed is saved
    // (a shortcut takes the Shift / Ctrl / Alt held with it), Escape leaves the setting as it is. KeyCapture does the waiting.
    VisualElement RebindButton(ConfigEntryBase entry, bool shortcut)
    {
        Func<string> shown = () => shortcut ? ShortcutLabel((KeyboardShortcut)entry.BoxedValue) : KeyCaptureRules.KeyName((KeyCode)entry.BoxedValue);
        Button button = null;
        button = UiControls.Button(shown(), () =>
        {
            button.text = "Press a key...  (Esc cancels)";
            KeyCapture.Begin(shortcut, (main, mods) =>
            {
                Write(entry, shortcut ? (object)new KeyboardShortcut(main, mods.ToArray()) : main);
                button.text = shown();
            }, () => button.text = shown());
        }, false);
        button.name = "kydl-key";
        return button;
    }

    static string ShortcutLabel(KeyboardShortcut s)
    {
        return KeyCaptureRules.ShortcutText(s.MainKey, new List<KeyCode>(s.Modifiers));
    }

    VisualElement ColorEditor(ConfigEntryBase entry)
    {
        Color now = (Color)entry.BoxedValue;
        Action<string> set;
        VisualElement picker = UiControls.ColorPicker("#" + ColorUtility.ToHtmlStringRGB(now), hex =>
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex, out c)) return;
            c.a = ((Color)entry.BoxedValue).a;                                  // the picker has no alpha: keep the entry's
            Write(entry, c);
        }, out set);
        picker.name = "kydl-color";
        return picker;
    }

    // --- writing and reading entries --------------------------------------------------------------------------------------
    void Write(ConfigEntryBase entry, object value)
    {
        if (value == null || Equals(entry.BoxedValue, value)) return;
        _writing = true;
        try { entry.BoxedValue = value; }
        finally { _writing = false; }
        ModLog.Verbose("ConfigPage: " + entry.Definition.Section + " / " + entry.Definition.Key + " = " + entry.GetSerializedValue());
    }

    static bool Range(ConfigEntryBase entry, out double min, out double max)
    {
        min = max = 0;
        AcceptableValueBase av = entry.Description == null ? null : entry.Description.AcceptableValues;
        if (av == null) return false;
        var lo = av.GetType().GetProperty("MinValue");
        var hi = av.GetType().GetProperty("MaxValue");
        if (lo == null || hi == null) return false;
        min = Convert.ToDouble(lo.GetValue(av, null));
        max = Convert.ToDouble(hi.GetValue(av, null));
        return true;
    }

    static IList<string> ListOf(ConfigEntryBase entry)
    {
        AcceptableValueBase av = entry.Description == null ? null : entry.Description.AcceptableValues;
        if (av == null) return null;
        var p = av.GetType().GetProperty("AcceptableValues");
        var values = p == null ? null : p.GetValue(av, null) as string[];
        return values;
    }
}
