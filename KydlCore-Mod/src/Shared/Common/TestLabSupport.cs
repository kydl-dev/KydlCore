using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace KydlCore.Common;

/// <summary>
/// KydlCore D-084: the attribute DrakesTestLab finds by NAME. The mod does not reference DrakesTestLab; a static method with this attribute becomes a test hook
/// (`{ "do": "hook", "name": "<Name>", "args": {...} }` in a suite). One copy, in KydlCore.dll; every mod uses it.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestHookAttribute : Attribute
{
    public TestHookAttribute(string name) { Name = name; }
    public string Name { get; }
}

/// <summary>Helpers the per-mod Testing/TestHooks.cs files share (KydlCore D-084).</summary>
public static class TestLabSupport
{
    /// <summary>
    /// Runs every static, parameterless, string-returning `Startup_*` / `World_*` method of the mod's InGameTests class (the ones Kydl.GameProbe runs) and sums the verdicts.
    /// Expected outcome: "PASS: n passed, m skipped" or "FAIL: k of n failed - Name: detail; ..." (a suite asserts `notContains: "FAIL"`). Each verdict is also written to the log.
    /// </summary>
    public static string RunInGameTests(Type tests, bool includeStartup = true)
    {
        if (tests == null) return "FAIL: no InGameTests class";
        var ctx = ModContexts.For(tests);
        int pass = 0, skip = 0, fail = 0;
        var bad = new StringBuilder();
        foreach (var m in tests.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            bool startup = m.Name.StartsWith("Startup_", StringComparison.Ordinal), world = m.Name.StartsWith("World_", StringComparison.Ordinal);
            if (!(startup && includeStartup) && !world) continue;
            if (m.GetParameters().Length != 0 || m.ReturnType != typeof(string)) continue;
            string r;
            try { r = (string)m.Invoke(null, null); }
            catch (Exception e) { r = "FAIL: exception " + (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message; }
            r = r ?? "FAIL: null";
            if (r.StartsWith("PASS", StringComparison.Ordinal)) pass++;
            else if (r.StartsWith("SKIP", StringComparison.Ordinal)) skip++;
            else { fail++; if (bad.Length < 600) bad.Append(bad.Length > 0 ? "; " : "").Append(m.Name).Append(": ").Append(r.Length > 160 ? r.Substring(0, 160) : r); }
            ModLog.Info(ctx, "TestLab self-test " + m.Name + " -> " + (r.Length > 200 ? r.Substring(0, 200) : r));
        }
        if (pass + skip + fail == 0) return "FAIL: " + tests.Name + " has no Startup_* / World_* methods";
        return fail == 0 ? "PASS: " + pass + " passed, " + skip + " skipped" : "FAIL: " + fail + " of " + (pass + skip + fail) + " failed - " + bad;
    }
}