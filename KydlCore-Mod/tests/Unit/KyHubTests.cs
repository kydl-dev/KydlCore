using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace KydlCore.Tests.Unit
{
    /// <summary>
    /// KyHub / KyTab (PLAN T-034, DESIGN 3): what a mod can do with the public hub API without the game: add, update and remove tabs, name itself,
    /// get the TabShown event, and have wrong input refused. The page builders are plain delegates (a UI page is built in game). The hub window
    /// itself (Open / key / lock) is game code and is not tested here. Reached in the BUILT mod DLL by reflection (like WindowStack).
    /// Every test uses its own mod GUID: KyHub is static state.
    /// </summary>
    [TestFixture]
    public class KyHubTests
    {
        static readonly Type Hub = ModAssembly.Type("KydlCore.Features.Hub.Contracts.KyHub");
        static readonly Type TabType = ModAssembly.Type("KydlCore.Features.Hub.Contracts.KyTab");
        static readonly Type ModelType = ModAssembly.Type("KydlCore.Ui.HubModel");
        static readonly MethodInfo AddTabM = Hub.GetMethod("AddTab");
        static readonly Type BuildType = AddTabM.GetParameters()[3].ParameterType;      // Func<VisualElement>

        static string Guid(string name) { return "com.kydldev.KyHubTest" + name; }

        // a page builder that returns null (no page): the unit host has no UI; a counter tells how often it was called
        static Delegate Builder(int[] calls)
        {
            Expression count = Expression.PostIncrementAssign(Expression.ArrayAccess(Expression.Constant(calls), Expression.Constant(0)));
            var body = Expression.Block(count, Expression.Constant(null, BuildType.GetGenericArguments()[0]));
            return Expression.Lambda(BuildType, body).Compile();
        }

        static object AddTab(string mod, string id, string title, Delegate build, int order = 0)
        {
            return Call("AddTab", mod, id, title, build, order);
        }

        static object Call(string name, params object[] args)
        {
            try { return Hub.GetMethod(name).Invoke(null, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static object Model() { return Hub.GetField("Model", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); }
        static object ModelCall(string name, params object[] args) { return ModelType.GetMethod(name).Invoke(Model(), args); }
        static string[] TabIds(string mod) { return (string[])ModelCall("TabIds", mod); }
        static string Prop(object o, string name) { return Convert.ToString(o.GetType().GetProperty(name).GetValue(o, null)); }

        [Test]
        public void AddTab_ReturnsTheTab_WithItsData()
        {
            string g = Guid("A");
            var tab = AddTab(g, "look", "Look", Builder(new int[1]), 3);
            Assert.That(tab, Is.Not.Null);
            Assert.That(Prop(tab, "ModGuid"), Is.EqualTo(g));
            Assert.That(Prop(tab, "Id"), Is.EqualTo("look"));
            Assert.That(Prop(tab, "Title"), Is.EqualTo("Look"));
            Assert.That(Prop(tab, "Order"), Is.EqualTo("3"));
        }

        [Test]
        public void AddTab_RegistersTheTabInTheHubModel()
        {
            string g = Guid("B");
            AddTab(g, "one", "One", Builder(new int[1]));
            Assert.That(TabIds(g), Is.EqualTo(new[] { "one" }));
            Assert.That((string)ModelCall("TabTitle", g, "one"), Is.EqualTo("One"));
        }

        [Test]
        public void AddTab_OrderDecidesTheRowOrder()
        {
            string g = Guid("C");
            AddTab(g, "late", "Late", Builder(new int[1]), 5);
            AddTab(g, "early", "Early", Builder(new int[1]), 1);
            Assert.That(TabIds(g), Is.EqualTo(new[] { "early", "late" }));
        }

        [Test]
        public void AddTab_SameId_UpdatesTheTitleAndKeepsOneTab_AndReturnsTheSameObject()
        {
            string g = Guid("D");
            var first = AddTab(g, "t", "Old", Builder(new int[1]));
            var second = AddTab(g, "t", "New", Builder(new int[1]), 2);
            Assert.That(second, Is.SameAs(first));
            Assert.That(Prop(first, "Title"), Is.EqualTo("New"));
            Assert.That(Prop(first, "Order"), Is.EqualTo("2"));
            Assert.That(TabIds(g), Is.EqualTo(new[] { "t" }));
        }

        [Test]
        public void AddTab_BlankTitle_ShowsTheId()
        {
            string g = Guid("E");
            var tab = AddTab(g, "general", "  ", Builder(new int[1]));
            Assert.That(Prop(tab, "Title"), Is.EqualTo("general"));
        }

        [Test]
        public void AddTab_WrongInput_IsRefused_AndChangesNothing()
        {
            string g = Guid("F");
            Assert.That(AddTab(null, "id", "T", Builder(new int[1])), Is.Null);
            Assert.That(AddTab("", "id", "T", Builder(new int[1])), Is.Null);
            Assert.That(AddTab(g, null, "T", Builder(new int[1])), Is.Null);
            Assert.That(AddTab(g, "", "T", Builder(new int[1])), Is.Null);
            Assert.That(AddTab(g, "id", "T", null), Is.Null);
            Assert.That(TabIds(g), Is.EqualTo(new[] { "settings" }), "no tab registered: the hub shows its one automatic Settings tab");
        }

        [Test]
        public void RemoveTab_RemovesIt_AndSaysSo()
        {
            string g = Guid("G");
            AddTab(g, "a", "A", Builder(new int[1]));
            AddTab(g, "b", "B", Builder(new int[1]));
            Assert.That(Call("RemoveTab", g, "a"), Is.True);
            Assert.That(TabIds(g), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void RemoveTab_UnknownTab_IsFalse()
        {
            Assert.That(Call("RemoveTab", Guid("H"), "none"), Is.False);
            Assert.That(Call("RemoveTab", null, null), Is.False);
        }

        [Test]
        public void RemoveTab_ThenAddAgain_GivesANewTab()
        {
            string g = Guid("I");
            var first = AddTab(g, "t", "T", Builder(new int[1]));
            Call("RemoveTab", g, "t");
            var again = AddTab(g, "t", "T", Builder(new int[1]));
            Assert.That(again, Is.Not.SameAs(first));
            Assert.That(TabIds(g), Is.EqualTo(new[] { "t" }));
        }

        // a page builder that records which mod the shared UI log says is drawing (KydlCore.Ui.UiLog.Owner, D-071)
        static Delegate OwnerRecorder(string[] seen)
        {
            Expression read = Expression.Assign(Expression.ArrayAccess(Expression.Constant(seen), Expression.Constant(0)),
                Expression.Field(null, ModAssembly.Type("KydlCore.Ui.UiLog").GetField("Owner")));
            var body = Expression.Block(read, Expression.Constant(null, BuildType.GetGenericArguments()[0]));
            return Expression.Lambda(BuildType, body).Compile();
        }

        [Test]
        public void BuildPage_RunsTheBuilder_WithTheModAsLogOwner_AndRestoresIt()
        {
            string g = Guid("Owner");
            var seen = new string[1];
            AddTab(g, "p", "P", OwnerRecorder(seen));
            var build = Hub.GetMethod("BuildPage", BindingFlags.Static | BindingFlags.NonPublic);
            build.Invoke(null, new object[] { g, "p" });
            Assert.That(seen[0], Is.EqualTo(g));
            Assert.That(ModAssembly.GetStatic("KydlCore.Ui.UiLog", "Owner"), Is.Null, "the owner is back to none after the page is built");
        }

        [Test]
        public void BuildPage_CallsTheBuilderEveryTime()
        {
            string g = Guid("J");
            var calls = new int[1];
            AddTab(g, "p", "P", Builder(calls));
            var build = Hub.GetMethod("BuildPage", BindingFlags.Static | BindingFlags.NonPublic);
            build.Invoke(null, new object[] { g, "p" });
            build.Invoke(null, new object[] { g, "p" });
            Assert.That(calls[0], Is.EqualTo(2));
        }

        [Test]
        public void BuildPage_UnknownTab_IsNull_AndABuilderThatThrowsIsNull()
        {
            string g = Guid("K");
            var build = Hub.GetMethod("BuildPage", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(build.Invoke(null, new object[] { g, "none" }), Is.Null);
            var boom = Expression.Lambda(BuildType, Expression.Throw(Expression.New(typeof(InvalidOperationException)), BuildType.GetGenericArguments()[0])).Compile();
            AddTab(g, "boom", "Boom", boom);
            Assert.That(build.Invoke(null, new object[] { g, "boom" }), Is.Null);
        }

        [Test]
        public void BuildPage_AfterRemove_IsNull()
        {
            string g = Guid("L");
            var calls = new int[1];
            AddTab(g, "p", "P", Builder(calls));
            Call("RemoveTab", g, "p");
            Hub.GetMethod("BuildPage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { g, "p" });
            Assert.That(calls[0], Is.EqualTo(0));
        }

        [Test]
        public void SameTabId_InTwoMods_StaysApart()
        {
            string a = Guid("M1"), b = Guid("M2");
            var ca = new int[1];
            var cb = new int[1];
            AddTab(a, "t", "T", Builder(ca));
            AddTab(b, "t", "T", Builder(cb));
            Hub.GetMethod("BuildPage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { a, "t" });
            Assert.That(ca[0], Is.EqualTo(1));
            Assert.That(cb[0], Is.EqualTo(0));
        }

        [Test]
        public void BuildPage_GuidAndIdAreNotJustGluedTogether()
        {
            string a = Guid("R"), b = Guid("R") + "y";          // (a, "yz") and (b, "z") are the same text when glued
            var ca = new int[1];
            var cb = new int[1];
            AddTab(a, "yz", "A", Builder(ca));
            AddTab(b, "z", "B", Builder(cb));
            Hub.GetMethod("BuildPage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { a, "yz" });
            Assert.That(ca[0], Is.EqualTo(1));
            Assert.That(cb[0], Is.EqualTo(0));
        }

        [Test]
        public void SetModTitle_AfterTheModIsListed_ChangesItsTitle()
        {
            string g = Guid("N");
            ModelCall("AddMod", g, "Plugin Name");
            Call("SetModTitle", g, "My Title", null);
            Assert.That((string)ModelCall("ModTitle", g), Is.EqualTo("My Title"));
            Assert.That((string)Hub.GetMethod("TitleOf", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { g }), Is.EqualTo("My Title"));
        }

        [Test]
        public void SetModTitle_BeforeTheModIsListed_IsKeptForLater_AndDoesNotListTheMod()
        {
            string g = Guid("O");
            Call("SetModTitle", g, "Early Title", null);
            Assert.That((string)ModelCall("ModTitle", g), Is.Null);
            Assert.That((string)Hub.GetMethod("TitleOf", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { g }), Is.EqualTo("Early Title"));
        }

        [Test]
        public void SetModTitle_NotAKydlDevGuid_IsRefused()
        {
            Call("SetModTitle", "com.other.Mod", "Nope", null);
            Call("SetModTitle", null, "Nope", null);
            Assert.That((string)Hub.GetMethod("TitleOf", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "com.other.Mod" }), Is.Null);
        }

        [Test]
        public void TitleOf_UnknownMod_IsNull()
        {
            Assert.That(Hub.GetMethod("TitleOf", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Guid("P") }), Is.Null);
        }

        [Test]
        public void RaiseTabShown_CallsEveryHandler_AndOneThatThrowsDoesNotStopTheRest()
        {
            var seen = new List<string>();
            Action<string, string> bad = (m, t) => { throw new InvalidOperationException("handler bug"); };
            Action<string, string> good = (m, t) => seen.Add(m + "|" + t);
            var ev = Hub.GetEvent("TabShown");
            ev.AddEventHandler(null, bad);
            ev.AddEventHandler(null, good);
            try { Hub.GetMethod("RaiseTabShown", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "g", "t" }); }
            finally { ev.RemoveEventHandler(null, bad); ev.RemoveEventHandler(null, good); }
            Assert.That(seen, Is.EqualTo(new[] { "g|t" }));
        }

        [Test]
        public void RaiseTabShown_WithNoHandler_DoesNothing()
        {
            Assert.DoesNotThrow(() => Hub.GetMethod("RaiseTabShown", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "g", "t" }));
        }

        [Test]
        public void ConfigEntries_WithNoFile_IsNull()
        {
            Assert.That(Hub.GetMethod("ConfigEntries").Invoke(null, new object[] { null, new string[0] }), Is.Null);
        }

        [Test]
        public void IsOpen_IsFalse_AndCloseWithNothingOpenIsHarmless()
        {
            Assert.That(Hub.GetProperty("IsOpen").GetValue(null, null), Is.False);
            Assert.DoesNotThrow(() => Call("Close"));
        }

        [Test]
        public void TabRefresh_WithNoHubOpen_IsHarmless()
        {
            var tab = AddTab(Guid("Q"), "t", "T", Builder(new int[1]));
            Assert.DoesNotThrow(() => TabType.GetMethod("Refresh").Invoke(tab, null));
        }
    }
}
