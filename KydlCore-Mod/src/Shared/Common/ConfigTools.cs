using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using UnityEngine;

namespace KydlCore.Common;

/// <summary>
/// SHARED helpers around the player's BepInEx config (lessons from DrainTarPit):
///  * CommaFix: "3,5" typed by hand (European habit) was read by BepInEx as 35. Capture the file before the first Bind, Apply after the last.
///  * DropRemoved: entries that are no longer bound (removed / moved to a Tunable) are dropped, so the file only lists what a player can change.
///  * Watcher: BepInEx does not watch the .cfg file; edits on disk are reloaded live (SettingChanged fires for every changed entry).
/// Rule (user): config descriptions never mention older versions, old names or old values.
/// </summary>
public static class ConfigTools
{
    static readonly Regex Section = new(@"^\s*\[(?<s>[^\]]+)\]\s*$");
    static readonly Regex CommaNumber = new(@"^\s*(?<k>[^=#]+?)\s*=\s*(?<v>-?\d+,\d+)\s*$");

    /// The config file's lines as they were before binding (null when there is no file yet). Call BEFORE the first Bind.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string[] Capture(ConfigFile c)
    {
        var ctx = ModContexts.For(Assembly.GetCallingAssembly());
        try { return c != null && !string.IsNullOrEmpty(c.ConfigFilePath) && File.Exists(c.ConfigFilePath) ? File.ReadAllLines(c.ConfigFilePath) : null; }
        catch (IOException e) { ModLog.Warn(ctx, "Config comma check skipped (file not readable): " + e.Message); return null; }
        catch (UnauthorizedAccessException e) { ModLog.Warn(ctx, "Config comma check skipped (no access): " + e.Message); return null; }
    }

    /// Call AFTER every Bind. Returns the number of float / double entries whose comma was read as the decimal point.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ApplyCommaFix(ConfigFile c, string[] lines)
    {
        var ctx = ModContexts.For(Assembly.GetCallingAssembly());
        int fixedCount = 0;
        if (c == null || lines == null) { ModLog.Verbose(ctx, "ConfigTools: no saved config lines, comma check not needed"); return fixedCount; }
        string section = "";
        foreach (var line in lines)
        {
            var sm = Section.Match(line);
            if (sm.Success) { section = sm.Groups["s"].Value.Trim(); continue; }
            var m = CommaNumber.Match(line);
            if (!m.Success) continue;
            var def = new ConfigDefinition(section, m.Groups["k"].Value);
            if (!c.ContainsKey(def)) continue;
            var entry = c[def];
            if (entry.SettingType != typeof(float) && entry.SettingType != typeof(double)) continue;
            double meant;
            if (!double.TryParse(m.Groups["v"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out meant)) continue;
            var was = entry.BoxedValue;
            entry.BoxedValue = entry.SettingType == typeof(float) ? (object)(float)meant : meant;
            fixedCount++;
            ModLog.Warn(ctx, "Config [" + section + "] " + def.Key + " = " + m.Groups["v"].Value + ": a comma is read as the decimal point -> " +
                        Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture) + " (BepInEx had read " +
                        Convert.ToString(was, CultureInfo.InvariantCulture) + "). Please use a dot in the config file.");
        }
        ModLog.Verbose(ctx, "ConfigTools: comma check done, " + fixedCount + " value(s) corrected");
        return fixedCount;
    }

    /// Entries that are not bound any more are dropped from the file. Returns the number of dropped entries.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int DropRemoved(ConfigFile c)
    {
        var ctx = ModContexts.For(Assembly.GetCallingAssembly());
        int dropped = 0;
        try
        {
            // the property is not public in every BepInEx build
            var orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var dict = orphans != null ? orphans.GetValue(c, null) as IDictionary : null;
            if (dict != null) { dropped = dict.Count; dict.Clear(); }
            else ModLog.Verbose(ctx, "ConfigTools: OrphanedEntries not found in this BepInEx, old entries stay in the file");
            c.Save();
            if (dropped > 0) ModLog.Info(ctx, "Config: " + dropped + " entry(ies) that are no longer settings were removed from the file");
        }
        catch (TargetInvocationException e) { ModLog.Warn(ctx, "Config: old entries could not be removed: " + e.InnerException?.Message); }
        catch (IOException e) { ModLog.Warn(ctx, "Config: the file could not be saved after removing old entries: " + e.Message); }
        return dropped;
    }
}

/// <summary>
/// SHARED. Reloads the config when the .cfg file is saved on disk. The watcher only sets a flag (it runs on another thread); Tick (from
/// Plugin.Update) reloads on the main thread 0.5 s later, because editors save in several writes. Dispose from Plugin.OnDestroy.
/// </summary>
public sealed class ConfigReloader : IDisposable
{
    readonly ConfigFile _config;
    readonly ModContext _ctx;
    FileSystemWatcher _watcher;
    volatile bool _changed;
    float _reloadAt;
    int _retries;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ConfigReloader(ConfigFile config)
    {
        _ctx = ModContexts.For(Assembly.GetCallingAssembly());
        _config = config;
        // PROJECT_RULES 17 (hot reload) needs proof in the log: every changed player setting is written with its new value, whether it
        // came from the .cfg file, Configuration Manager or code (RearViewCamera T-G009: FOV / mirror changes left no trace).
        _config.SettingChanged += (s, e) =>
        {
            var entry = e != null ? e.ChangedSetting : null;
            if (entry == null) return;
            ModLog.Info(_ctx, "Config: [" + entry.Definition.Section + "] " + entry.Definition.Key + " = " + entry.BoxedValue + " (applied live)");
        };
        try
        {
            var path = config.ConfigFilePath;
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            _watcher.Changed += (s, e) => _changed = true;
            _watcher.Created += (s, e) => _changed = true;
            _watcher.Renamed += (s, e) => _changed = true;     // some editors save to a temp file and rename it over the config
            _watcher.EnableRaisingEvents = true;
            ModLog.Verbose(_ctx, "ConfigReloader: watching " + path);
        }
        catch (ArgumentException e) { ModLog.Warn(_ctx, "Config file watcher not started (bad path; edits to the .cfg need a restart): " + e.Message); }
        catch (IOException e) { ModLog.Warn(_ctx, "Config file watcher not started (edits to the .cfg need a restart): " + e.Message); }
    }

    public bool Watching => _watcher != null && _watcher.EnableRaisingEvents;

    /// Every frame. Expected outcome: true when the config was reloaded in this call.
    public bool Tick()
    {
        if (_changed) { _changed = false; _reloadAt = Time.realtimeSinceStartup + 0.5f; _retries = 0; }
        if (_reloadAt <= 0f || Time.realtimeSinceStartup < _reloadAt) return false;
        _reloadAt = 0f;
        try
        {
            if (!File.Exists(_config.ConfigFilePath)) { ModLog.Verbose(_ctx, "ConfigReloader: the file is gone, nothing reloaded"); return false; }
            _config.Reload();                      // raises SettingChanged for every entry whose value differs
            ModLog.Info(_ctx, "Config file changed on disk - reloaded.");
            return true;
        }
        catch (IOException e)
        {
            if (++_retries <= 3) { _reloadAt = Time.realtimeSinceStartup + 1f; ModLog.Verbose(_ctx, "ConfigReloader: file locked, retry " + _retries + ": " + e.Message); }
            else ModLog.Warn(_ctx, "Config reload failed: " + e.Message);
            return false;
        }
    }

    public void Dispose()
    {
        if (_watcher == null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
        ModLog.Verbose(_ctx, "ConfigReloader: stopped");
    }
}