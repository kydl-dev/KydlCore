using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FsCheck;

namespace KydlCore.Tests.Unit
{
    /// <summary>
    /// HubModel (PLAN T-031, DESIGN 5): row 1 = the loaded KydlDev mods ordered by title, row 2 = the selected mod's registered tabs (or one
    /// automatic "Settings" tab), exactly one mod and one tab selected while a mod exists, Changed fires once per real change. The property test
    /// runs random commands and checks the invariants. The class is reached in the BUILT mod DLL by reflection, so Stryker can mutate it.
    /// </summary>
    [TestFixture]
    public class HubModelTests
    {
        const string A = "com.kydldev.Alpha";
        const string B = "com.kydldev.Beta";
        const string C = "com.kydldev.Gamma";

        /// <summary>The mod's internal KydlCore.Ui.HubModel, called through reflection; exceptions are not wrapped.</summary>
        sealed class Hub
        {
            static readonly Type T = ModAssembly.Type("KydlCore.Ui.HubModel");
            readonly object _o = Activator.CreateInstance(T);
            public int Changes;

            public Hub() { T.GetEvent("Changed").AddEventHandler(_o, (Action)(() => Changes++)); }

            public static string ModPrefix { get { return (string)T.GetField("ModPrefix").GetValue(null); } }
            public static string AutoTabId { get { return (string)T.GetField("AutoTabId").GetValue(null); } }
            public static string AutoTabTitle { get { return (string)T.GetField("AutoTabTitle").GetValue(null); } }
            public static bool IsHubGuid(string g) { return (bool)T.GetMethod("IsHubGuid").Invoke(null, new object[] { g }); }
            public string SelectedMod { get { return (string)T.GetProperty("SelectedMod").GetValue(_o); } }
            public string SelectedTab { get { return (string)T.GetProperty("SelectedTab").GetValue(_o); } }
            public int ModCount { get { return (int)T.GetProperty("ModCount").GetValue(_o); } }
            public bool AddMod(string g, string t) { return (bool)Call("AddMod", g, t); }
            public bool RemoveMod(string g) { return (bool)Call("RemoveMod", g); }
            public string[] ModGuids() { return (string[])Call("ModGuids"); }
            public string ModTitle(string g) { return (string)Call("ModTitle", g); }
            public bool AddTab(string g, string id, string title, int order) { return (bool)Call("AddTab", g, id, title, order); }
            public bool RemoveTab(string g, string id) { return (bool)Call("RemoveTab", g, id); }
            public string[] TabIds(string g) { return (string[])Call("TabIds", g); }
            public string TabTitle(string g, string id) { return (string)Call("TabTitle", g, id); }
            public bool IsAutoTab(string g) { return (bool)Call("IsAutoTab", g); }
            public bool SelectMod(string g) { return (bool)Call("SelectMod", g); }
            public bool SelectTab(string id) { return (bool)Call("SelectTab", id); }
            public bool Select(string g, string id) { return (bool)Call("Select", g, id); }

            object Call(string name, params object[] args)
            {
                try { return T.GetMethod(name).Invoke(_o, args); }
                catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
            }
        }

        static string Join(string[] a) { return string.Join(",", a); }

        [Test]
        public void Constants_AreWhatTheDesignSays()
        {
            Assert.That(Hub.ModPrefix, Is.EqualTo("com.kydldev."));
            Assert.That(Hub.AutoTabId, Is.EqualTo("settings"));
            Assert.That(Hub.AutoTabTitle, Is.EqualTo("Settings"));
        }

        [TestCase("com.kydldev.ChestDecor", true)]
        [TestCase("COM.KYDLDEV.ChestDecor", true)]
        [TestCase("com.kydldev.", false)]
        [TestCase("com.kydldev", false)]
        [TestCase("000.kydldev.GameProbe", false)]
        [TestCase("com.other.Mod", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsHubGuid_OnlyKydlDevPluginsWithAName(string guid, bool expected)
        {
            Assert.That(Hub.IsHubGuid(guid), Is.EqualTo(expected), guid);
        }

        [Test]
        public void New_IsEmpty_NothingSelected_AnyModShowsTheAutoTab()
        {
            var h = new Hub();
            Assert.That(h.ModCount, Is.EqualTo(0));
            Assert.That(h.ModGuids(), Is.Empty);
            Assert.That(h.SelectedMod, Is.Null);
            Assert.That(h.SelectedTab, Is.Null);
            Assert.That(Join(h.TabIds(A)), Is.EqualTo("settings"));
            Assert.That(h.IsAutoTab(A), Is.True);
            Assert.That(h.Changes, Is.EqualTo(0));
        }

        [Test]
        public void AddMod_FirstModIsSelected_WithItsAutoTab_OneChangedEvent()
        {
            var h = new Hub();
            Assert.That(h.AddMod(A, "Alpha"), Is.True);
            Assert.That(h.SelectedMod, Is.EqualTo(A));
            Assert.That(h.SelectedTab, Is.EqualTo("settings"));
            Assert.That(h.ModTitle(A), Is.EqualTo("Alpha"));
            Assert.That(h.Changes, Is.EqualTo(1));
        }

        [Test]
        public void AddMod_RejectsGuidsTheHubDoesNotList_NothingChanges()
        {
            var h = new Hub();
            Assert.That(h.AddMod("000.kydldev.GameProbe", "Probe"), Is.False);
            Assert.That(h.AddMod("com.other.Mod", "Other"), Is.False);
            Assert.That(h.AddMod(null, "x"), Is.False);
            Assert.That(h.ModCount, Is.EqualTo(0));
            Assert.That(h.Changes, Is.EqualTo(0));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void AddMod_BlankTitle_ShowsTheGuid(string title)
        {
            var h = new Hub();
            h.AddMod(A, title);
            Assert.That(h.ModTitle(A), Is.EqualTo(A));
        }

        [Test]
        public void AddMod_SameGuidAgain_UpdatesTheTitle_NoDuplicate_EventOnlyWhenItChanged()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            int before = h.Changes;
            Assert.That(h.AddMod(A, "Alpha"), Is.True);
            Assert.That(h.Changes, Is.EqualTo(before), "same title: no event");
            Assert.That(h.AddMod(A, "Alpha Two"), Is.True);
            Assert.That(h.Changes, Is.EqualTo(before + 1));
            Assert.That(h.ModCount, Is.EqualTo(1));
            Assert.That(h.ModTitle(A), Is.EqualTo("Alpha Two"));
        }

        [Test]
        public void ModTitle_UnknownOrInvalid_IsNull()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            Assert.That(h.ModTitle(B), Is.Null);
            Assert.That(h.ModTitle(null), Is.Null);
            Assert.That(h.ModTitle(""), Is.Null);
        }

        [Test]
        public void Row1_IsOrderedByTitleIgnoringCase_ThenByGuid()
        {
            var h = new Hub();
            h.AddMod(C, "zeta");
            h.AddMod(A, "Mid");
            h.AddMod(B, "alpha");
            h.AddMod("com.kydldev.Same2", "same");
            h.AddMod("com.kydldev.Same1", "SAME");
            Assert.That(Join(h.ModGuids()), Is.EqualTo(B + "," + A + ",com.kydldev.Same1,com.kydldev.Same2," + C));
        }

        [Test]
        public void ALaterModThatSortsFirst_DoesNotStealTheSelection()
        {
            var h = new Hub();
            h.AddMod(C, "Gamma");
            h.AddMod(A, "Alpha");
            Assert.That(h.SelectedMod, Is.EqualTo(C));
            Assert.That(Join(h.ModGuids()), Is.EqualTo(A + "," + C));
        }

        [Test]
        public void RemoveMod_Selected_SelectionMovesToTheFirstLeft_LastOneLeavesNothingSelected()
        {
            var h = new Hub();
            h.AddMod(B, "Beta"); h.AddMod(A, "Alpha"); h.AddMod(C, "Gamma");
            h.Select(B, null);
            int before = h.Changes;
            Assert.That(h.RemoveMod(B), Is.True);
            Assert.That(h.SelectedMod, Is.EqualTo(A));
            Assert.That(h.SelectedTab, Is.EqualTo("settings"));
            Assert.That(h.Changes, Is.EqualTo(before + 1));
            Assert.That(h.RemoveMod(A), Is.True);
            Assert.That(h.RemoveMod(C), Is.True);
            Assert.That(h.SelectedMod, Is.Null);
            Assert.That(h.SelectedTab, Is.Null);
            Assert.That(h.ModCount, Is.EqualTo(0));
        }

        [Test]
        public void RemoveMod_Selected_NextModShowsItsFirstTab_NotTheTabNumberOfTheRemovedOne()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha"); h.AddMod(B, "Beta");
            h.AddTab(A, "x", "X", 0); h.AddTab(A, "y", "Y", 1);
            h.AddTab(B, "x", "X", 0); h.AddTab(B, "y", "Y", 1);
            h.SelectTab("y");
            h.RemoveMod(A);
            Assert.That(h.SelectedMod, Is.EqualTo(B));
            Assert.That(h.SelectedTab, Is.EqualTo("x"), "B was never shown, so its first tab, whatever tab A had");
        }

        [Test]
        public void RemoveMod_Selected_NextModShowsTheTabLastSelectedThere()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha"); h.AddMod(B, "Beta");
            h.AddTab(B, "x", "X", 0); h.AddTab(B, "y", "Y", 1);
            h.Select(B, "y");
            h.SelectMod(A);
            h.RemoveMod(A);
            Assert.That(h.SelectedMod, Is.EqualTo(B));
            Assert.That(h.SelectedTab, Is.EqualTo("y"));
        }

        [Test]
        public void RemoveMod_NotSelected_KeepsTheSelection_UnknownIsFalseAndSilent()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha"); h.AddMod(B, "Beta");
            int before = h.Changes;
            Assert.That(h.RemoveMod(B), Is.True);
            Assert.That(h.SelectedMod, Is.EqualTo(A));
            Assert.That(h.RemoveMod(B), Is.False);
            Assert.That(h.RemoveMod(null), Is.False);
            Assert.That(h.RemoveMod(""), Is.False);
            Assert.That(h.Changes, Is.EqualTo(before + 1));
        }

        [Test]
        public void AddTab_Row2_IsOrderedByOrder_ThenByRegistration_AndReplacesTheAutoTab()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            Assert.That(h.IsAutoTab(A), Is.True);
            h.AddTab(A, "look", "Look", 5);
            h.AddTab(A, "general", "General", 1);
            h.AddTab(A, "camera", "Camera", 5);
            Assert.That(Join(h.TabIds(A)), Is.EqualTo("general,look,camera"));
            Assert.That(h.IsAutoTab(A), Is.False);
            Assert.That(h.TabIds(A), Does.Not.Contain("settings"));
        }

        [Test]
        public void AddTab_RegisteredBeforeTheModIsKnown_IsKept()
        {
            var h = new Hub();
            Assert.That(h.AddTab(A, "look", "Look", 0), Is.True);
            Assert.That(h.SelectedMod, Is.Null);
            h.AddMod(A, "Alpha");
            Assert.That(h.SelectedTab, Is.EqualTo("look"));
        }

        [Test]
        public void AddTab_SameIdAgain_UpdatesTitleAndOrder_KeepsOneEntry()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            h.AddTab(A, "a", "A", 1);
            h.AddTab(A, "b", "B", 2);
            int before = h.Changes;
            Assert.That(h.AddTab(A, "a", "A", 1), Is.True);
            Assert.That(h.Changes, Is.EqualTo(before), "same values: no event");
            Assert.That(h.AddTab(A, "a", "A2", 3), Is.True);
            Assert.That(Join(h.TabIds(A)), Is.EqualTo("b,a"));
            Assert.That(h.TabTitle(A, "a"), Is.EqualTo("A2"));
            Assert.That(h.Changes, Is.EqualTo(before + 1));
            h.AddTab(A, "a", "A2", 0);
            Assert.That(Join(h.TabIds(A)), Is.EqualTo("a,b"), "an order change alone counts as a change");
        }

        [TestCase(null, "id")]
        [TestCase("", "id")]
        [TestCase(A, null)]
        [TestCase(A, "")]
        public void AddTab_InvalidGuidOrId_IsFalse_NothingChanges(string guid, string id)
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            int before = h.Changes;
            Assert.That(h.AddTab(guid, id, "T", 0), Is.False);
            Assert.That(h.IsAutoTab(A), Is.True);
            Assert.That(h.Changes, Is.EqualTo(before));
        }

        [Test]
        public void TabTitle_Registered_Auto_BlankShowsId_UnknownIsNull()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha"); h.AddMod(B, "Beta");
            h.AddTab(B, "look", "  ", 0);
            h.AddTab(B, "cam", "Camera", 1);
            Assert.That(h.TabTitle(A, "settings"), Is.EqualTo("Settings"));
            Assert.That(h.TabTitle(A, "look"), Is.Null);
            Assert.That(h.TabTitle(B, "look"), Is.EqualTo("look"));
            Assert.That(h.TabTitle(B, "cam"), Is.EqualTo("Camera"));
            Assert.That(h.TabTitle(B, "nope"), Is.Null);
            Assert.That(h.TabTitle(B, null), Is.Null);
            Assert.That(h.TabTitle(null, "settings"), Is.EqualTo("Settings"));
        }

        [Test]
        public void ARegisteredTabCalledSettings_IsAnOrdinaryTab()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            h.AddTab(A, "settings", "My settings", 0);
            Assert.That(h.IsAutoTab(A), Is.False);
            Assert.That(h.TabTitle(A, "settings"), Is.EqualTo("My settings"));
            Assert.That(h.RemoveTab(A, "settings"), Is.True);
            Assert.That(h.IsAutoTab(A), Is.True);
            Assert.That(h.TabTitle(A, "settings"), Is.EqualTo("Settings"));
        }

        [Test]
        public void FirstRegisteredTab_IsSelected_WhenTheAutoTabWasShown()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            Assert.That(h.SelectedTab, Is.EqualTo("settings"));
            h.AddTab(A, "look", "Look", 0);
            Assert.That(h.SelectedTab, Is.EqualTo("look"));
        }

        [Test]
        public void RemoveTab_SelectedTab_MovesToTheFirstLeft_LastOneBringsBackTheAutoTab()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            h.AddTab(A, "a", "A", 0); h.AddTab(A, "b", "B", 1); h.AddTab(A, "c", "C", 2);
            h.SelectTab("b");
            Assert.That(h.RemoveTab(A, "b"), Is.True);
            Assert.That(h.SelectedTab, Is.EqualTo("a"));
            Assert.That(h.RemoveTab(A, "a"), Is.True);
            Assert.That(h.RemoveTab(A, "c"), Is.True);
            Assert.That(h.SelectedTab, Is.EqualTo("settings"));
            Assert.That(Join(h.TabIds(A)), Is.EqualTo("settings"));
        }

        [Test]
        public void RemoveTab_Unknown_IsFalse_AndSilent()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            h.AddTab(A, "a", "A", 0);
            int before = h.Changes;
            Assert.That(h.RemoveTab(A, "zzz"), Is.False);
            Assert.That(h.RemoveTab(B, "a"), Is.False);
            Assert.That(h.RemoveTab(null, "a"), Is.False);
            Assert.That(h.RemoveTab(A, null), Is.False);
            Assert.That(h.RemoveTab(A, ""), Is.False);
            Assert.That(h.RemoveTab("", "a"), Is.False);
            Assert.That(h.Changes, Is.EqualTo(before));
        }

        [Test]
        public void RemoveTab_NotTheSelectedOne_KeepsTheSelection()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            h.AddTab(A, "a", "A", 0); h.AddTab(A, "b", "B", 1);
            h.SelectTab("b");
            int before = h.Changes;
            h.RemoveTab(A, "a");
            Assert.That(h.SelectedTab, Is.EqualTo("b"));
            Assert.That(h.Changes, Is.EqualTo(before + 1), "row 2 changed, so the view must redraw");
        }

        [Test]
        public void SelectMod_ShowsTheTabLastSelectedThere_FirstTabWhenNone()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha"); h.AddMod(B, "Beta");
            h.AddTab(A, "a1", "A1", 0); h.AddTab(A, "a2", "A2", 1);
            h.AddTab(B, "b1", "B1", 0); h.AddTab(B, "b2", "B2", 1);
            h.SelectTab("a2");
            Assert.That(h.SelectMod(B), Is.True);
            Assert.That(h.SelectedTab, Is.EqualTo("b1"));
            h.SelectTab("b2");
            Assert.That(h.SelectMod(A), Is.True);
            Assert.That(h.SelectedTab, Is.EqualTo("a2"));
            Assert.That(h.SelectMod(B), Is.True);
            Assert.That(h.SelectedTab, Is.EqualTo("b2"));
        }

        [Test]
        public void SelectMod_RememberedTabIsGone_FallsBackToTheFirst()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha"); h.AddMod(B, "Beta");
            h.AddTab(A, "a1", "A1", 0); h.AddTab(A, "a2", "A2", 1);
            h.SelectTab("a2");
            h.SelectMod(B);
            h.RemoveTab(A, "a2");
            h.SelectMod(A);
            Assert.That(h.SelectedTab, Is.EqualTo("a1"));
        }

        [Test]
        public void SelectMod_Unknown_IsFalse_NothingChanges()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");
            int before = h.Changes;
            Assert.That(h.SelectMod(B), Is.False);
            Assert.That(h.SelectMod(null), Is.False);
            Assert.That(h.SelectMod(""), Is.False);
            Assert.That(h.SelectedMod, Is.EqualTo(A));
            Assert.That(h.Changes, Is.EqualTo(before));
        }

        [Test]
        public void SelectTab_Unknown_IsFalse_NothingChanges_NoModIsFalse()
        {
            var h = new Hub();
            Assert.That(h.SelectTab("settings"), Is.False, "no mod selected");
            h.AddMod(A, "Alpha");
            h.AddTab(A, "a", "A", 0);
            int before = h.Changes;
            Assert.That(h.SelectTab("zzz"), Is.False);
            Assert.That(h.SelectTab(null), Is.False);
            Assert.That(h.SelectTab("settings"), Is.False, "the auto tab does not exist when the mod registered tabs");
            Assert.That(h.SelectedTab, Is.EqualTo("a"));
            Assert.That(h.Changes, Is.EqualTo(before));
        }

        [Test]
        public void Select_ModAndTab_AsKyHubOpenDoes_WrongTabOrModChangesNothing()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha"); h.AddMod(B, "Beta");
            h.AddTab(B, "x", "X", 0); h.AddTab(B, "y", "Y", 1);
            Assert.That(h.Select(B, "y"), Is.True);
            Assert.That(h.SelectedMod, Is.EqualTo(B));
            Assert.That(h.SelectedTab, Is.EqualTo("y"));
            int before = h.Changes;
            Assert.That(h.Select(B, "nope"), Is.False);
            Assert.That(h.Select(C, null), Is.False);
            Assert.That(h.Select(B, "y"), Is.True);
            Assert.That(h.Changes, Is.EqualTo(before), "selecting what is already selected is silent");
            Assert.That(h.SelectedMod, Is.EqualTo(B));
            Assert.That(h.SelectedTab, Is.EqualTo("y"));
        }

        [Test]
        public void Changed_FiresOncePerRealChange_NotForNoOps()
        {
            var h = new Hub();
            h.AddMod(A, "Alpha");                    // 1
            h.AddTab(A, "a", "A", 0);                // 2 (row 2 and the selection change in the same call: still one event)
            h.AddTab(A, "b", "B", 1);                // 3
            h.SelectTab("b");                        // 4
            Assert.That(h.Changes, Is.EqualTo(4));
            h.SelectTab("b"); h.AddTab(A, "b", "B", 1); h.AddMod(A, "Alpha"); h.RemoveTab(A, "zzz");
            Assert.That(h.Changes, Is.EqualTo(4));
        }

        [Test]
        public void Changed_HandlerRunsAfterTheStateChanged()
        {
            var t = ModAssembly.Type("KydlCore.Ui.HubModel");
            var o = Activator.CreateInstance(t);
            string seen = null;
            t.GetEvent("Changed").AddEventHandler(o, (Action)(() => seen = (string)t.GetProperty("SelectedMod").GetValue(o)));
            t.GetMethod("AddMod").Invoke(o, new object[] { A, "Alpha" });
            Assert.That(seen, Is.EqualTo(A));
        }

        [Test]
        public void Property_RandomCommands_KeepTheHubInvariants()
        {
            var mods = new[] { A, B, C, "com.kydldev.Delta" };
            var titles = new[] { "alpha", "Beta", "BETA", "delta", "" };
            var tabs = new[] { "t0", "t1", "t2", "settings" };
            var commands = Gen.Choose(0, 7 * 4 * 4 * 5 - 1);
            Props.Check(Prop.ForAll(Gen.ListOf(commands).Select(l => l.ToArray()).ToArbitrary(), cmds =>
            {
                var h = new Hub();
                var model = new Dictionary<string, HashSet<string>>();      // mod guid -> registered tab ids (the plain model)
                var present = new HashSet<string>();
                foreach (int c in cmds)
                {
                    int op = c % 7, m = (c / 7) % 4, t = (c / 28) % 4, ti = (c / 112) % 5;
                    string mod = mods[m], tab = tabs[t];
                    switch (op)
                    {
                        case 0: if (!h.AddMod(mod, titles[ti])) return false; present.Add(mod); break;
                        case 1: if (h.RemoveMod(mod) != present.Remove(mod)) return false; break;
                        case 2:
                            if (!h.AddTab(mod, tab, titles[ti], ti - 2)) return false;
                            HashSet<string> set; if (!model.TryGetValue(mod, out set)) model[mod] = set = new HashSet<string>(); set.Add(tab); break;
                        case 3:
                            bool had = model.ContainsKey(mod) && model[mod].Remove(tab);
                            if (h.RemoveTab(mod, tab) != had) return false; break;
                        case 4: if (h.SelectMod(mod) != present.Contains(mod)) return false; break;
                        case 5: h.SelectTab(tab); break;
                        default: h.Select(mod, tab); break;
                    }
                    if (!Invariants(h, present, model, mods)) return false;
                }
                return true;
            }));
        }

        static bool Invariants(Hub h, HashSet<string> present, Dictionary<string, HashSet<string>> model, string[] allMods)
        {
            string[] row1 = h.ModGuids();
            if (row1.Length != present.Count || row1.Distinct().Count() != row1.Length || !row1.All(present.Contains) || h.ModCount != present.Count) return false;
            var titleOrder = row1.Select(g => h.ModTitle(g)).ToArray();
            for (int i = 1; i < row1.Length; i++)
            {
                int cmp = string.Compare(titleOrder[i - 1], titleOrder[i], StringComparison.OrdinalIgnoreCase);
                if (cmp > 0 || (cmp == 0 && string.CompareOrdinal(row1[i - 1], row1[i]) > 0)) return false;
            }
            foreach (var g in allMods)
            {
                int registered = model.ContainsKey(g) ? model[g].Count : 0;
                string[] ids = h.TabIds(g);
                if (ids.Length != Math.Max(1, registered) || ids.Distinct().Count() != ids.Length) return false;
                if (h.IsAutoTab(g) != (registered == 0)) return false;
                if (ids.Any(id => h.TabTitle(g, id) == null)) return false;
            }
            if (row1.Length == 0) return h.SelectedMod == null && h.SelectedTab == null;
            return h.SelectedMod != null && present.Contains(h.SelectedMod) && h.SelectedTab != null && h.TabIds(h.SelectedMod).Contains(h.SelectedTab);
        }
    }
}
