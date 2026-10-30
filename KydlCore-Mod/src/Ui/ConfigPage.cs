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

        readonly ConfigFile _file;
        readonly VisualElement _root = new VisualElement { name = "kydl-config-page" };
        volatile bool _dirty;
        bool _writing;
        IVisualElementScheduledItem _poll;

        internal ConfigPage(ConfigFile file)
        {
            _file = file;
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
                if (!ConfigControlMap.IsHidden(Tags(entry))) shown.Add(entry);
            }
            foreach (var section in shown.GroupBy(e => e.Definition.Section))        // GroupBy keeps the order of first appearance
            {
                _root.Add(UiControls.Heading(section.Key));
                foreach (ConfigEntryBase entry in section) _root.Add(Control(entry));
            }
            if (shown.Count == 0) _root.Add(UiControls.Note("This mod has no settings."));
            ModLog.Verbose("ConfigPage: " + shown.Count + " setting(s) shown");
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
            box.Add(control.name == "kydl-toggle" ? control : UiControls.Row(key, control));
            if (locked) box.Add(UiControls.Note("Admin only: only the host / an admin can change this."));
            string text = entry.Description == null ? null : entry.Description.Description;
            if (!string.IsNullOrEmpty(text)) box.Add(UiControls.Note(text));
            ModLog.Verbose("ConfigPage: control for " + entry.Definition.Section + " / " + key + " (" + entry.SettingType.Name + ")" + (locked ? " locked" : ""));
            return box;
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
                case ControlKind.Slider: return UiControls.Slider((float)min, (float)max, Convert.ToSingle(entry.BoxedValue), ConfigControlMap.SliderFormat(type),
                    v => Write(entry, ConfigControlMap.Clamp(type, v, min, max)));
                case ControlKind.Number: return UiControls.TextField(ConfigControlMap.Format(type, entry.BoxedValue), TextMax, t => Write(entry, ConfigControlMap.Parse(type, t)));
                case ControlKind.Text: return UiControls.TextField((string)entry.BoxedValue, TextMax, t => Write(entry, t));
                case ControlKind.Dropdown: return Dropdown(entry, list);
                case ControlKind.Color: return ColorEditor(entry);
                default: return UiControls.Note(entry.GetSerializedValue());
            }
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
