using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Kydl.Common;
using Kydl.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace KydlCore.Ui
{
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
        const float FontScale = 0.8f;             // every text of the page is this much smaller than the kit's size (user 2026-10-30, D-060)
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
            if (_poll != null) _poll.Pause();
            ModLog.Verbose("ConfigPage: disposed");
        }

        void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (_writing) return;                 // our own write: the control already shows the value
            _dirty = true;
        }

        void Poll()
        {
            if (!_dirty) return;
            _dirty = false;
            ModLog.Verbose("ConfigPage: a setting changed outside the page, redrawing");
            Fill();
        }

        // --- drawing -------------------------------------------------------------------------------------------------------
        void Fill()
        {
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
            bool locked = ConfigControlMap.IsAdminOnly(Tags(entry)) && !MayEditAdminOnly();
            VisualElement control = locked ? LockedValue(entry) : Make(entry, key);
            box.Add(control.name == "kydl-toggle" ? control : Row(key, control));
            if (locked) box.Add(UiControls.Note("Admin only: only the host / an admin can change this."));
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
            l.style.width = LabelWidth; l.style.flexShrink = 0; l.style.whiteSpace = WhiteSpace.NoWrap;
            r.Add(l);
            bool stretch = control.name == "kydl-slider" || control.name == "kydl-color";
            control.style.flexGrow = stretch ? 1f : 0f;
            control.style.flexShrink = stretch ? 1f : 0f;
            if (!stretch) control.style.alignSelf = Align.FlexStart;
            r.Add(control);
            return r;
        }

        // Makes every text of the page smaller by FontScale (the kit's controls set their sizes inline, so the inline value is scaled).
        static void ScaleFonts(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(e =>
            {
                StyleLength fs = e.style.fontSize;
                if (fs.keyword == StyleKeyword.Undefined && fs.value.value > 0f) e.style.fontSize = fs.value.value * FontScale;
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
                case ControlKind.Number: return Sized(UiControls.TextField(ConfigControlMap.Format(type, entry.BoxedValue), TextMax, t => Write(entry, ConfigControlMap.Parse(type, t))), 160f);
                case ControlKind.Text: return Sized(UiControls.TextField((string)entry.BoxedValue, TextMax, t => Write(entry, t)), 360f);
                case ControlKind.Dropdown: return Dropdown(entry, list);
                case ControlKind.Color: return ColorEditor(entry);
                default: return UiControls.Note(entry.GetSerializedValue());
            }
        }

        static VisualElement Sized(VisualElement box, float width)
        {
            box.style.width = width;
            return box;
        }

        VisualElement Slider(ConfigEntryBase entry, double min, double max)
        {
            Type type = entry.SettingType;
            var slider = UiControls.Slider((float)min, (float)max, Convert.ToSingle(entry.BoxedValue), ConfigControlMap.SliderFormat(type), v => Write(entry, ConfigControlMap.Clamp(type, v, min, max)));
            slider.name = "kydl-slider";
            return slider;
        }

        VisualElement Toggle(ConfigEntryBase entry, string key)
        {
            var t = UiControls.Toggle(key, (bool)entry.BoxedValue, on => Write(entry, on));
            t.name = "kydl-toggle";
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
}
