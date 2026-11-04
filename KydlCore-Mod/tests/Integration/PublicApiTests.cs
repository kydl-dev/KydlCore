using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace KydlCore.Tests.Integration;

/// <summary>
/// The public API of KydlCore.dll is a contract: every other KydlDev mod builds against it (D-040 / D-042, PLAN T-050). This test lists every public
/// (and protected) type and member of the BUILT dll with Mono.Cecil (metadata only, nothing is loaded) and compares the list with
/// PublicApi.approved.txt. Any difference fails, in both directions: a new public member must be approved on purpose, and a removed or renamed one
/// is a breaking change for the mods. To approve: read PublicApi.received.txt (written next to the approved file by a failing run), then copy it over
/// PublicApi.approved.txt and say why in DECISIONS. Replaces the hand-written list of PublicUiSurfaceTests.
/// </summary>
[TestFixture]
public class PublicApiTests
{
    const string ApprovedName = "PublicApi.approved.txt", ReceivedName = "PublicApi.received.txt";

    static string Dir { get { return Path.Combine(TestPaths.TestsRoot, "Integration"); } }

    /// One line per public type and per public / protected member, sorted ordinally, LF line ends, with a trailing newline.
    internal static string Describe(ModuleDefinition module)
    {
        var lines = new List<string>();
        foreach (var type in AllTypes(module.Types).Where(IsExposed).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add(Kind(type) + " " + type.FullName);
            var members = new List<string>();
            foreach (var f in type.Fields.Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly)) members.Add("  field " + type.FullName + "::" + f.Name + " : " + f.FieldType.FullName + ConstantOf(f));
            foreach (var m in type.Methods.Where(Exposed)) members.Add("  method " + m.FullName);
            foreach (var e in type.Events.Where(e => Exposed(e.AddMethod) || Exposed(e.RemoveMethod))) members.Add("  event " + type.FullName + "::" + e.Name + " : " + e.EventType.FullName);
            lines.AddRange(members.OrderBy(x => x, StringComparer.Ordinal));
        }
        return string.Join("\n", lines) + "\n";
    }

    /// A constant is part of the contract (it is compiled into the caller), except the version, which changes with every release.
    static string ConstantOf(FieldDefinition f) { return f.HasConstant && f.Name != "ModVersion" ? " = " + f.Constant : ""; }

    static bool Exposed(MethodDefinition m) { return m != null && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly); }

    static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var t in types)
        {
            yield return t;
            foreach (var n in AllTypes(t.NestedTypes)) yield return n;
        }
    }

    /// A type is part of the API when it and every type around it is public (nested public / protected inside a public type).
    static bool IsExposed(TypeDefinition t)
    {
        if (t.FullName.Contains("<")) return false;                 // compiler generated
        if (!t.IsNested) return t.IsPublic;
        return (t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly) && IsExposed(t.DeclaringType);
    }

    static string Kind(TypeDefinition t)
    {
        if (t.IsEnum) return "enum";
        if (t.IsInterface) return "interface";
        if (t.IsValueType) return "struct";
        if (t.BaseType != null && t.BaseType.FullName == "System.MulticastDelegate") return "delegate";
        if (t.IsAbstract && t.IsSealed) return "static class";
        if (t.IsAbstract) return "abstract class";
        return t.IsSealed ? "sealed class" : "class";
    }

    static ModuleDefinition Read()
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(TestPaths.RefDir);
        return ModuleDefinition.ReadModule(TestPaths.ModDll, new ReaderParameters { AssemblyResolver = resolver });
    }

    [Test]
    public void PublicApi_MatchesApprovedFile()
    {
        string received;
        using (var module = Read()) received = Describe(module);
        var approvedPath = Path.Combine(Dir, ApprovedName);
        var receivedPath = Path.Combine(Dir, ReceivedName);
        var approved = File.Exists(approvedPath) ? File.ReadAllText(approvedPath).Replace("\r\n", "\n") : null;
        if (approved == received)
        {
            if (File.Exists(receivedPath)) File.Delete(receivedPath);
            return;
        }
        File.WriteAllText(receivedPath, received);
        var a = approved == null ? new string[0] : approved.Split('\n');
        var r = received.Split('\n');
        var added = r.Except(a).Take(30).Select(x => "  + " + x.Trim());
        var removed = a.Except(r).Take(30).Select(x => "  - " + x.Trim());
        Assert.Fail("The public API of KydlCore.dll changed" + (approved == null ? " (no " + ApprovedName + " yet)" : "") + ". Review " + receivedPath +
                    ", then copy it over " + approvedPath + " and write the reason into DECISIONS (a removed or renamed member breaks the mods).\n" +
                    string.Join("\n", added.Concat(removed)));
    }

    // T-098 (f): the described-page API lets a mod say WHAT to show, never how it looks: no public member of the page types may talk about font, size, colour or style.
    [Test]
    public void PageApi_HasNoLookMembers()
    {
        using (var module = Read())
        {
            var bad = new System.Collections.Generic.List<string>();
            foreach (var t in module.Types)
            {
                if (!t.IsPublic || t.Namespace != "KydlCore" || !(t.Name == "KyPage" || t.Name == "KySection" || t.Name == "KyCell" || (t.Name.StartsWith("Ky") && t.Name.EndsWith("Row")))) continue;
                foreach (var m in t.Methods.Where(x => x.IsPublic).Cast<IMemberDefinition>().Concat(t.Properties).Concat(t.Fields.Where(f => f.IsPublic)))
                    foreach (var word in new[] { "Font", "Size", "Color", "Colour", "Style", "Width", "Height" })
                        if (m.Name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) bad.Add(t.Name + "." + m.Name);
            }
            Assert.That(bad, Is.Empty, "the page API must not let a mod set how it looks");
        }
    }

    [Test]
    public void ApprovedFile_ContainsTheKitTypes()
    {
        var approved = File.ReadAllText(Path.Combine(Dir, ApprovedName)).Replace("\r\n", "\n");
        foreach (var type in new[] { "KydlCore.Ui.UiKit", "KydlCore.Ui.UiControls", "KydlCore.Ui.UiScroll", "KydlCore.Ui.UiLog", "KydlCore.Common.UiInputLock", "KydlCore.Features.Hub.Contracts.KyHub", "KydlCore.Features.Items.Contracts.KyItems", "KydlCore.Features.Items.Contracts.KyEntry" })
            Assert.That(approved, Does.Contain(" " + type + "\n"), type + " must be public API");
    }

    [Test]
    public void KitMembers_KeptInternalByDecision_StayInternal()
    {
        // D-042: used only inside the kit, so never part of the approved API.
        var approved = File.ReadAllText(Path.Combine(Dir, ApprovedName));
        foreach (var member in new[] { " KydlCore.Ui.UiKit::Reset(", " KydlCore.Ui.UiKit::Font(", " KydlCore.Ui.UiKit::get_TitleSize(", "  field KydlCore.Ui.UiKit::TitleSize " })
            Assert.That(approved, Does.Not.Contain(member), member + " is internal by D-042");
    }
}
