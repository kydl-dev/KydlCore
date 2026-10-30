using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace KydlCore.Tests.Integration
{
    /// <summary>
    /// Contract of the shared UI kit (PLAN T-020h, D-040 / D-041 / D-042): every other KydlDev mod builds against KydlCore.dll and uses these
    /// Kydl.Ui / Kydl.Common types and members, so they must be PUBLIC in the BUILT dll (KydlCore.csproj defines KYDL_CORE_PUBLIC). A member that falls back
    /// to internal would only show up as a compile error in a moved mod; this fails here first. Read with Mono.Cecil (metadata only), like
    /// PatchTargetsTests: nothing is loaded or run. The list grows only by a decision (T-050 turns it into PublicApi.approved.txt).
    /// </summary>
    [TestFixture]
    public class PublicUiSurfaceTests
    {
        const string Ui = "Kydl.Ui.", Common = "Kydl.Common.";

        /// Every type the mods use, with the members that must be public (fields, properties, methods, constructors; overloads all count).
        static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            { Ui + "UiControls", new[] { "Button", "Field", "Heading", "Note", "Row", "Line", "TextField", "Toggle", "Slider", "Dropdown", "ColorPicker" } },
            { Ui + "UiKit", new[]
                {
                    "Panel", "PanelLight", "PanelHover", "Border", "Gold", "GoldDim", "Text", "TextDim", "Track", "OnDark", "NameColor", "ValueColor", "TabColor",
                    "SectionSize", "FontSize", "ButtonSize", "SmallSize", "NameSize", "Owner", "Settings", "Root",
                    "Radius", "Borders", "Pad", "Fade", "Label", "Window"
                } },
            { Ui + "UiScroll", new[] { ".ctor", "Root", "Content", "ScrollBy", "ScrollTo" } },
            { Ui + "UiLog", new[] { "VerboseSink", "WarnSink", "ErrorSink", "Verbose", "Warn", "Error" } },
            { Common + "UiInputLock", new[] { "Count", "Install", "Hold", "Release" } },
            { "KydlCore.KyHub", new[] { "AddTab", "RemoveTab", "SetModTitle", "ConfigEntries", "Open", "Close", "IsOpen", "add_TabShown", "remove_TabShown" } },       // T-034: the hub API of every mod
            { "KydlCore.KyTab", new[] { "ModGuid", "Id", "Title", "Order", "Refresh" } },
            { Ui + "UiSlider", new string[0] },         // returned by UiControls.Slider as a plain VisualElement: the type is public, no member is needed
            { Ui + "UiColorPicker", new string[0] }
        };

        /// Members that D-042 deliberately kept internal (not used outside the kit). Making one public is a decision: change this list with it.
        static readonly Dictionary<string, string[]> StayInternal = new Dictionary<string, string[]>
        {
            { Ui + "UiKit", new[] { "Reset", "Font", "TitleSize" } }
        };

        static ModuleDefinition _mod;

        static ModuleDefinition Mod()
        {
            if (_mod != null) return _mod;
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(TestPaths.RefDir);
            return _mod = ModuleDefinition.ReadModule(TestPaths.ModDll, new ReaderParameters { AssemblyResolver = resolver });
        }

        static TypeDefinition Find(string fullName) { return Mod().Types.FirstOrDefault(t => t.FullName == fullName); }

        /// Every member called `name` (a property = its getter / setter, a constructor = ".ctor") and whether it is public.
        static List<KeyValuePair<string, bool>> Members(TypeDefinition t, string name)
        {
            var found = new List<KeyValuePair<string, bool>>();
            foreach (var m in t.Methods.Where(m => m.Name == name)) found.Add(new KeyValuePair<string, bool>("method " + m.FullName, m.IsPublic));
            foreach (var f in t.Fields.Where(f => f.Name == name)) found.Add(new KeyValuePair<string, bool>("field " + f.FullName, f.IsPublic));
            foreach (var p in t.Properties.Where(p => p.Name == name))
            {
                if (p.GetMethod != null) found.Add(new KeyValuePair<string, bool>("getter " + p.GetMethod.FullName, p.GetMethod.IsPublic));
            }
            return found;
        }

        [Test]
        public void KitTypes_ArePublic()
        {
            var bad = Required.Keys.Where(n => Find(n) == null || !Find(n).IsPublic).ToList();
            Assert.That(bad, Is.Empty, "not public (or missing) in the built KydlCore.dll - is KYDL_CORE_PUBLIC defined in KydlCore.csproj? " + string.Join(", ", bad));
        }

        [Test]
        public void KitMembers_ArePublic()
        {
            var problems = new List<string>();
            foreach (var pair in Required)
            {
                var type = Find(pair.Key);
                if (type == null) { problems.Add(pair.Key + ": type missing"); continue; }
                foreach (var name in pair.Value)
                {
                    var found = Members(type, name);
                    if (found.Count == 0) problems.Add(pair.Key + "." + name + ": not found");
                    foreach (var f in found.Where(x => !x.Value)) problems.Add(pair.Key + "." + name + ": not public (" + f.Key + ")");
                }
            }
            Assert.That(problems, Is.Empty, "members the other mods use must be public in KydlCore.dll:\n  " + string.Join("\n  ", problems));
        }

        [Test]
        public void KitMembers_KeptInternalByDecision_StayInternal()
        {
            var problems = new List<string>();
            foreach (var pair in StayInternal)
            {
                var type = Find(pair.Key);
                if (type == null) { problems.Add(pair.Key + ": type missing"); continue; }
                foreach (var name in pair.Value)
                {
                    var found = Members(type, name);
                    if (found.Count == 0) problems.Add(pair.Key + "." + name + ": not found (renamed? update StayInternal)");
                    foreach (var f in found.Where(x => x.Value)) problems.Add(pair.Key + "." + name + ": is public now (" + f.Key + "); D-042 kept it internal");
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n  ", problems));
        }
    }
}
