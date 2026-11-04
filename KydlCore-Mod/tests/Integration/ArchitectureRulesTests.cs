using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KydlCore.Tests.Integration;

/// <summary>
/// The KydlDev spec / Architecture V2 rules, checked on the sources (MIGRATION_DECISIONS A3-A9, B10): pure layers reference no game or
/// engine, Harmony patches live only in src/Integrations/Harmony, KydlCore stays a hard dependency, exact network versions, the
/// architecture marker the lab wrapper looks for. Keep these green; a deliberate deviation is written in docs/Architecture.md AND here.
/// </summary>
[TestFixture]
public class ArchitectureRulesTests
{
    static readonly Regex ForbiddenUsing = new Regex(@"^\s*using\s+(?:(?:UnityEngine|HarmonyLib|BepInEx|Jotunn)\b|KydlCore\.(?:Common|Ui|Integrations|Bootstrap|Presentation)\b|KydlCore\s*;)", RegexOptions.Multiline);
    static readonly Regex ForbiddenType = new Regex(@"\b(GameObject|Component|MonoBehaviour|Transform|Vector3|Quaternion|Character|Humanoid|MonsterAI|BaseAI|ZNetView|ZDO|ZNet|ZNetScene|Player|Time\.time|Physics|ModLog|KyHub)\b");
    static readonly Regex LineComment = new Regex(@"//.*$", RegexOptions.Multiline);
    static readonly Regex StringLiteral = new Regex(@"""([^""\\]|\\.)*""");

    static string Src { get { return TestPaths.Src; } }
    static string SharedCore { get { return Path.Combine(TestPaths.Src, "Shared", "Core"); } }

    static IEnumerable<string> PureFiles()
    {
        var files = new List<string>();
        var core = Path.Combine(Src, "Core");
        if (Directory.Exists(core)) files.AddRange(Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories));
        foreach (var dir in Directory.GetDirectories(Path.Combine(Src, "Features"), "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "Domain" || name == "Application" || name == "Execution") files.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
        }
        return files.Distinct();
    }

    static List<string> Violations(IEnumerable<string> files)
    {
        var bad = new List<string>();
        foreach (var f in files)
        {
            string code = StringLiteral.Replace(LineComment.Replace(File.ReadAllText(f), ""), "\"\"");
            var u = ForbiddenUsing.Match(code);
            if (u.Success) bad.Add(Path.GetFileName(f) + ": " + u.Value.Trim());
            var t = ForbiddenType.Match(code);
            if (t.Success) bad.Add(Path.GetFileName(f) + ": names " + t.Value);
        }
        return bad;
    }

    [Test] public void PureLayers_ReferenceNoGameOrEngine()
    {
        Assert.That(PureFiles(), Is.Not.Empty, "src/Features/<F>/{Domain,Application,Execution}");
        Assert.That(Violations(PureFiles()), Is.Empty, "the domain decides without the game - move game reads into the feature's Infrastructure adapter");
    }

    [Test] public void KydlCoreContracts_ArePure()
    {
        Assert.That(Directory.Exists(SharedCore), Is.True, SharedCore);
        Assert.That(Violations(Directory.GetFiles(SharedCore, "*.cs", SearchOption.AllDirectories)), Is.Empty, "src\\Shared\\Core (KydlCore.Core) must stay pure");
    }

    [Test] public void HarmonyPatches_LiveOnlyIn_IntegrationsHarmony()
    {
        var harmonyDir = Path.Combine(Src, "Integrations", "Harmony") + Path.DirectorySeparatorChar;
        var outside = Directory.GetFiles(Src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(harmonyDir) && Regex.IsMatch(LineComment.Replace(File.ReadAllText(f), ""), @"\[HarmonyPatch\b|\.Patch\("))
            .Select(Path.GetFileName).ToList();
        Assert.That(outside, Is.Empty, "MIGRATION_DECISIONS A5: Harmony patches go into src/Integrations/Harmony");
    }

    // KydlCore itself: it IS the shared runtime (compiles the UI kit, no KydlUseCore, no dependency on itself) and joins servers without it
    [Test] public void KydlCore_CompilesTheKit_VersionCheckOnlyExact_NoEnableAll()
    {
        string plugin = File.ReadAllText(Path.Combine(Src, "Bootstrap", "Plugin.cs"));
        string proj = File.ReadAllText(Directory.GetFiles(TestPaths.ModDir, "*.csproj").Single());
        Assert.That(proj, Does.Not.Contain("<KydlUseCore>true</KydlUseCore>"));
        Assert.That(Directory.Exists(Path.Combine(Src, "Shared", "Ui")), Is.True, "the UI kit is compiled from src/Shared/Ui (D-089)");
        Assert.That(proj, Does.Not.Match(@"shared\\Kydl\.(Core|Common|Ui)\b"), "no shared source is linked from Claude\\shared any more");
        Assert.That(plugin, Does.Contain("[NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Patch)]"), "MIGRATION_DECISIONS: KydlCore = VersionCheckOnly + Patch");
        string settings = File.ReadAllText(Path.Combine(Src, "Configuration", "ModSettings.cs"));
        Assert.That(Regex.IsMatch(settings, @"Bind\(\s*""General""\s*,\s*""EnableAll"""), Is.False, "MIGRATION_DECISIONS B12: KydlCore can never be switched off");
    }

    [Test] public void EveryFeature_HasAnAdapterImplementingIFeature()
    {
        foreach (var feature in Directory.GetDirectories(Path.Combine(Src, "Features")))
        {
            var infra = Path.Combine(feature, "Infrastructure");
            bool has = Directory.Exists(infra) && Directory.GetFiles(infra, "*.cs", SearchOption.AllDirectories).Any(f => Regex.IsMatch(File.ReadAllText(f), @":\s*IFeature\b"));
            Assert.That(has, Is.True, Path.GetFileName(feature) + ": Infrastructure/<X>Feature.cs implementing IFeature (MIGRATION_DECISIONS A4)");
        }
    }

    [Test] public void ArchitectureMarker_IsPresent()
    {
        Assert.That(File.ReadAllText(Path.Combine(Src, "Core", "Architecture.cs")), Does.Contain("Tag = \"KydlCore-KYDL-SPEC-V1\""));
    }

    /// The public API sits in the namespace of its folder (the KydlCore code already moved there; this supersedes the D-087 "keep namespace KydlCore" deviation, see D-089).
    [Test] public void PublicApi_UsesFolderNamespaces()
    {
        var expected = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Features/Hub/Contracts/KyHub.cs", "KydlCore.Features.Hub.Contracts" },
            { "Features/Items/Contracts/KyItems.cs", "KydlCore.Features.Items.Contracts" },
            { "Features/Items/Contracts/KyContribution.cs", "KydlCore.Features.Items.Contracts" },
            { "Bootstrap/Plugin.cs", "KydlCore.Bootstrap" },
            { "Shared/Core/Results/OperationResponse.cs", "KydlCore.Core" },
            { "Shared/Common/ModLog.cs", "KydlCore.Common" },
            { "Shared/Ui/UiKit.cs", "KydlCore.Ui" },
        };
        foreach (var kv in expected)
            Assert.That(Regex.IsMatch(File.ReadAllText(Path.Combine(Src, kv.Key)), @"^namespace " + Regex.Escape(kv.Value) + @"\s*;?\s*$", RegexOptions.Multiline), Is.True, kv.Key + " must declare namespace " + kv.Value);
    }
}
