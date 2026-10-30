using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace KydlCore.Ui
{
    /// <summary>The control the auto "Settings" page shows for one config entry.</summary>
    internal enum ControlKind
    {
        /// <summary>A type the page cannot edit: shown as read-only text.</summary>
        Unsupported,
        /// <summary>bool: a pill switch.</summary>
        Toggle,
        /// <summary>int / float / double with an AcceptableValueRange: slider + number box.</summary>
        Slider,
        /// <summary>A number without a range (or a long): a text box that only accepts numbers.</summary>
        Number,
        /// <summary>string: a text box.</summary>
        Text,
        /// <summary>An enum (not KeyCode) or a string with an AcceptableValueList: a drop-down.</summary>
        Dropdown,
        /// <summary>UnityEngine.Color: the colour editor.</summary>
        Color,
        /// <summary>UnityEngine.KeyCode: a press-to-rebind button (one key).</summary>
        Key,
        /// <summary>BepInEx KeyboardShortcut: a press-to-rebind button (a key with the Shift / Ctrl / Alt held with it).</summary>
        Shortcut
    }

    /// <summary>
    /// Pure rules of the auto "Settings" page (PLAN T-033, D-023): which control a config entry gets, how number text is read and shown, and how the
    /// BepInEx tags "Browsable = false" / "IsAdminOnly = true" are found. No Unity and no BepInEx types, no logging (like WindowStack): the page
    /// (ConfigPage) does the drawing and the writing through <c>ConfigEntryBase.BoxedValue</c>. Tags are read by member NAME, because the mods and
    /// Jotunn each ship their own <c>ConfigurationManagerAttributes</c> class (Jotunn's is internal; checked in Jotunn.dll 2026-10-30).
    /// </summary>
    internal static class ConfigControlMap
    {
        const string BrowsableName = "Browsable", AdminOnlyName = "IsAdminOnly", ColorTypeName = "UnityEngine.Color";
        const string KeyCodeTypeName = "UnityEngine.KeyCode", ShortcutTypeName = "BepInEx.Configuration.KeyboardShortcut";

        /// <summary>The control for a value type. `hasRange` = the entry has an AcceptableValueRange, `hasList` = an AcceptableValueList.</summary>
        public static ControlKind Classify(Type valueType, bool hasRange, bool hasList)
        {
            if (valueType == null) return ControlKind.Unsupported;
            if (valueType == typeof(bool)) return ControlKind.Toggle;
            if (valueType.FullName == KeyCodeTypeName) return ControlKind.Key;            // before the enums: a key is pressed, not picked from ~300 names
            if (valueType.FullName == ShortcutTypeName) return ControlKind.Shortcut;
            if (valueType.IsEnum) return ControlKind.Dropdown;
            if (valueType == typeof(string)) return hasList ? ControlKind.Dropdown : ControlKind.Text;
            if (valueType == typeof(long)) return ControlKind.Number;
            if (valueType == typeof(int) || valueType == typeof(float) || valueType == typeof(double)) return hasRange ? ControlKind.Slider : ControlKind.Number;
            if (valueType.FullName == ColorTypeName) return ControlKind.Color;
            return ControlKind.Unsupported;
        }

        /// <summary>True for the number types of the page: int, long, float, double.</summary>
        public static bool IsNumber(Type valueType)
        {
            return valueType == typeof(int) || valueType == typeof(long) || valueType == typeof(float) || valueType == typeof(double);
        }

        /// <summary>The text of a number as the page shows it: whole numbers plain, fractions with at most 2 decimals, always with a dot.</summary>
        public static string Format(Type valueType, object value)
        {
            if (value == null || !IsNumber(valueType)) return value == null ? "" : value.ToString();
            if (IsWhole(valueType)) return Convert.ToString(value, CultureInfo.InvariantCulture);       // not through double: a long keeps every digit
            return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>The number format for a slider of this type ("0" for whole numbers, else "0.##").</summary>
        public static string SliderFormat(Type valueType)
        {
            return IsWhole(valueType) ? "0" : "0.##";
        }

        /// <summary>
        /// Reads the text a player typed as a number of `valueType` (a dot or a comma as decimal sign, spaces around ignored). Null when it is not a
        /// finite number of that type; a whole-number type rejects a fraction and a value outside its range.
        /// </summary>
        public static object Parse(Type valueType, string text)
        {
            if (!IsNumber(valueType) || text == null) return null;
            string s = text.Trim().Replace(',', '.');
            if (IsWhole(valueType)) return ParseWhole(valueType, s);
            double d;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) || double.IsNaN(d) || double.IsInfinity(d)) return null;
            if (valueType == typeof(float)) return Math.Abs(d) > float.MaxValue ? null : (object)(float)d;
            return d;
        }

        /// <summary>`value` limited to min..max (the entry's range) and turned into `valueType`; whole-number types round to the nearest integer.</summary>
        public static object Clamp(Type valueType, double value, double min, double max)
        {
            double d = Math.Max(min, Math.Min(max, value));
            if (valueType == typeof(int)) return (int)Math.Round(d);
            if (valueType == typeof(long)) return (long)Math.Round(d);
            if (valueType == typeof(float)) return (float)d;
            return d;
        }

        /// <summary>True when a tag says Browsable = false: the entry is not shown on the page.</summary>
        public static bool IsHidden(IEnumerable<object> tags)
        {
            return Flag(tags, BrowsableName) == false;
        }

        /// <summary>True when a tag says IsAdminOnly = true: only an admin (or the host) may change the entry.</summary>
        public static bool IsAdminOnly(IEnumerable<object> tags)
        {
            return Flag(tags, AdminOnlyName) == true;
        }

        /// <summary>The first tag member called `name` (public or not, field or property) of type bool / bool?; null when no tag has one or it is null.</summary>
        static bool? Flag(IEnumerable<object> tags, string name)
        {
            if (tags == null) return null;
            foreach (object tag in tags)
            {
                bool? v = tag == null ? null : MemberFlag(tag, name);
                if (v.HasValue) return v;
            }
            return null;
        }

        static bool? MemberFlag(object tag, string name)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = tag.GetType();
            FieldInfo f = t.GetField(name, all);
            if (f != null) return f.GetValue(tag) as bool?;
            PropertyInfo p = t.GetProperty(name, all);
            return p != null && p.GetIndexParameters().Length == 0 ? p.GetValue(tag, null) as bool? : null;
        }

        static bool IsWhole(Type valueType)
        {
            return valueType == typeof(int) || valueType == typeof(long);
        }

        static object ParseWhole(Type valueType, string s)
        {
            long n;
            if (!long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n)) return null;
            if (valueType == typeof(long)) return n;
            return n < int.MinValue || n > int.MaxValue ? null : (object)(int)n;
        }
    }
}
