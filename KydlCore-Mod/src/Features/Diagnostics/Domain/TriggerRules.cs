using System;
using System.Text;
using System.Text.RegularExpressions;

namespace KydlCore.Features.Diagnostics.Domain;

/// <summary>
/// Reads the log lines that start a flight recorder dump. DrakesTestLab has no event a mod can subscribe to (TESTLAB-AUTHORING.md: it writes
/// its report and its own log lines), so a test result is read from the lab's line "Test &lt;Suite&gt;/&lt;test&gt;: Passed" or
/// "Test &lt;Suite&gt;/&lt;test&gt;: Failed - &lt;why&gt;", and the end of every test from "Test state reset.".
/// Pure C#: no Unity, no game types, no logging, so it is unit tested.
/// </summary>
internal static class TriggerRules
{
    /// <summary>The BepInEx log source name of DrakesTestLab.</summary>
    internal const string LabSource = "DrakesTestLab";

    /// <summary>The lab's line after every test.</summary>
    internal const string LabReset = "Test state reset.";

    static readonly Regex LabResult = new(@"^Test (?<test>\S.*?): (?<result>Passed|Failed)(?: - (?<why>.*))?$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>Is this the lab's "Test X: Passed / Failed" line? Gives the test name, whether it failed and the reason the lab gave.</summary>
    internal static bool TryLabResult(string source, string text, out string test, out bool failed, out string why)
    {
        test = null;
        failed = false;
        why = null;
        if (source != LabSource || string.IsNullOrEmpty(text))
        {
            return test != null;
        }

        Match m;
        try
        {
            m = LabResult.Match(text.Trim());
        }
        catch (RegexMatchTimeoutException)
        {
            return test != null;
        }

        if (!m.Success)
        {
            return test != null;
        }

        test = m.Groups["test"].Value;
        failed = m.Groups["result"].Value == "Failed";
        why = m.Groups["why"].Success ? m.Groups["why"].Value : "";
        return test != null;
    }

    /// <summary>Is this the lab's line after every test ("Test state reset.")?</summary>
    internal static bool IsLabReset(string source, string text) =>
        source == LabSource && text != null && text.Trim() == LabReset;

    /// <summary>A file-name part made from a reason: letters, digits and '-' only, at most <paramref name="max"/> characters, never empty.</summary>
    internal static string Slug(string reason, int max = 40)
    {
        var sb = new StringBuilder();
        bool dash = false;
        foreach (char ch in reason ?? "")
        {
            if (char.IsLetterOrDigit(ch) && ch < 128)
            {
                sb.Append(char.ToLowerInvariant(ch));
                dash = false;
            }
            else if (!dash && sb.Length > 0)
            {
                sb.Append('-');
                dash = true;
            }

            if (sb.Length >= max)
            {
                break;
            }
        }

        string slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "dump" : slug;
    }

    /// <summary>The text cut to <paramref name="max"/> characters (with "..." when cut), first line only; never null.</summary>
    internal static string Short(string text, int max)
    {
        string first = (text ?? "").Split('\n')[0].TrimEnd('\r');
        return first.Length <= max ? first : first.Substring(0, Math.Max(0, max)) + "...";
    }
}
