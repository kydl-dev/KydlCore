using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FsCheck;

namespace KydlCore.Tests.Unit;

/// <summary>
/// KY-Items registry (PLAN T-040, DESIGN 6): the pure rules in <c>KyItemsModel</c> (one entry per prefab name, lookups, replace + changed flag,
/// which GUID is the user's). Finding the prefabs in the game (Jotunn, hammer, Register) is game code, tested in game. Reached in the BUILT mod DLL
/// by reflection (like WindowStack).
/// </summary>
[TestFixture]
public class KyItemsTests
{
    static readonly Type ModelType = ModAssembly.Type("KydlCore.Features.Items.Infrastructure.KyItemsModel");
    static readonly Type EntryType = ModAssembly.Type("KydlCore.Features.Items.Contracts.KyEntry");

    static object NewModel() { return Activator.CreateInstance(ModelType, true); }

    static object Entry(string prefab, string guid = "com.kydldev.A")
    {
        object e = Activator.CreateInstance(EntryType);
        EntryType.GetField("Prefab").SetValue(e, prefab);
        EntryType.GetField("ModGuid").SetValue(e, guid);
        return e;
    }

    static object Call(object model, string name, params object[] args)
    {
        try { return ModelType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    static bool Add(object model, object entry) { return (bool)Call(model, "Add", entry); }
    static int Count(object model) { return ((System.Collections.ICollection)ModelType.GetProperty("All", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model, null)).Count; }
    static string[] Names(object model)
    {
        return ((System.Collections.IEnumerable)ModelType.GetProperty("All", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model, null))
            .Cast<object>().Select(e => (string)EntryType.GetField("Prefab").GetValue(e)).ToArray();
    }
    static bool IsUser(string guid) { return (bool)ModAssembly.Call("KydlCore.Features.Items.Infrastructure.KyItemsModel", "IsUserGuid", guid); }

    // ---- IsUserGuid ----------------------------------------------------------------------------------------------------------

    [TestCase("com.kydldev.DrainTarPit")]
    [TestCase("com.kydldev.KydlCore")]
    [TestCase("COM.KYDLDEV.WhipItUp")]
    [TestCase("com.kydldev.x")]
    public void UserGuids_Count(string guid) { Assert.That(IsUser(guid), Is.True); }

    [TestCase("com.kydldev.")]                // the prefix only
    [TestCase("com.kydldev")]
    [TestCase("com.someoneelse.Mod")]
    [TestCase("kydldev.Mod")]
    [TestCase("")]
    [TestCase(null)]
    public void OtherGuids_DoNotCount(string guid) { Assert.That(IsUser(guid), Is.False); }

    // ---- Add / Find ----------------------------------------------------------------------------------------------------------

    [Test]
    public void Add_NewPrefab_IsListed()
    {
        object m = NewModel();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Add(m, Entry("Cart")), Is.True);
            Assert.That(Count(m), Is.EqualTo(1));
        }
    }

    [Test]
    public void Add_SamePrefabAgain_IsRefused_AnyCase_FirstWins()
    {
        object m = NewModel();
        object first = Entry("Cart", "com.kydldev.A");
        Add(m, first);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Add(m, Entry("cart", "com.kydldev.B")), Is.False);
            Assert.That(Count(m), Is.EqualTo(1));
            Assert.That(Call(m, "Find", "CART"), Is.SameAs(first));
        }
    }

    [Test]
    public void Add_NullAndEmptyPrefab_AreRefused()
    {
        object m = NewModel();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Add(m, null), Is.False);
            Assert.That(Add(m, Entry(null)), Is.False);
            Assert.That(Add(m, Entry("")), Is.False);
            Assert.That(Count(m), Is.Zero);
        }
    }

    [Test]
    public void Find_UnknownAndNull_GiveNull()
    {
        object m = NewModel();
        Add(m, Entry("Cart"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Call(m, "Find", "Boat"), Is.Null);
            Assert.That(Call(m, "Find", new object[] { null }), Is.Null);
        }
    }

    [Test]
    public void All_KeepsTheOrderTheyWereAdded()
    {
        object m = NewModel();
        foreach (string n in new[] { "Zeta", "Alpha", "Mid" }) Add(m, Entry(n));
        Assert.That(Names(m), Is.EqualTo(new[] { "Zeta", "Alpha", "Mid" }));
    }

    // ---- ByMod ---------------------------------------------------------------------------------------------------------------

    [Test]
    public void ByMod_GivesOnlyThatModsEntries_AnyCase()
    {
        object m = NewModel();
        Add(m, Entry("A1", "com.kydldev.A")); Add(m, Entry("B1", "com.kydldev.B")); Add(m, Entry("A2", "com.kydldev.A"));
        var got = ((IEnumerable<object>)Call(m, "ByMod", "COM.kydldev.a")).Select(e => (string)EntryType.GetField("Prefab").GetValue(e)).ToArray();
        Assert.That(got, Is.EqualTo(new[] { "A1", "A2" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("com.kydldev.None")]
    public void ByMod_UnknownOrEmpty_IsEmpty(string guid)
    {
        object m = NewModel();
        Add(m, Entry("A1", ""));                  // an entry without a GUID is not found by an empty GUID
        Assert.That(((IEnumerable<object>)Call(m, "ByMod", guid)).Any(), Is.False);
    }

    // ---- ReplaceWith ---------------------------------------------------------------------------------------------------------

    static object ModelOf(params string[] names)
    {
        object m = NewModel();
        foreach (string n in names) Add(m, Entry(n));
        return m;
    }

    [Test]
    public void ReplaceWith_SameNames_IsNotAChange_ButTakesTheFreshEntries()
    {
        object m = ModelOf("A", "B");
        object next = NewModel();
        object freshA = Entry("a"); object freshB = Entry("b");        // same names, other case and other objects
        Add(next, freshB); Add(next, freshA);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((bool)Call(m, "ReplaceWith", next), Is.False);
            Assert.That(Call(m, "Find", "A"), Is.SameAs(freshA));
            Assert.That(Names(m), Is.EqualTo(new[] { "b", "a" }));
        }
    }

    [Test]
    public void ReplaceWith_NewName_IsAChange()
    {
        object m = ModelOf("A");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((bool)Call(m, "ReplaceWith", ModelOf("A", "B")), Is.True);
            Assert.That(Count(m), Is.EqualTo(2));
        }
    }

    [Test]
    public void ReplaceWith_GoneName_IsAChange()
    {
        object m = ModelOf("A", "B");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((bool)Call(m, "ReplaceWith", ModelOf("A")), Is.True);
            Assert.That(Count(m), Is.EqualTo(1));
        }
    }

    [Test]
    public void ReplaceWith_EmptyIntoEmpty_IsNoChange()
    {
        object m = NewModel();
        Assert.That((bool)Call(m, "ReplaceWith", NewModel()), Is.False);
    }

    // ---- properties ----------------------------------------------------------------------------------------------------------

    /// Whatever names are added, in any order and with repeats (any case): each name is listed once, the first one wins.
    [Test]
    public void Property_EachNameOnce_FirstWins()
    {
        var names = Gen.ListOf(Props.TextFrom("abcABC", 3)).Select(l => l.ToArray());
        Props.Check(Prop.ForAll(names.ToArbitrary(), list =>
        {
            object m = NewModel();
            var seen = new List<string>();
            foreach (string n in list)
            {
                bool expected = n.Length > 0 && !seen.Any(s => string.Equals(s, n, StringComparison.OrdinalIgnoreCase));
                if (Add(m, Entry(n)) != expected) return false;
                if (expected) seen.Add(n);
            }
            return Names(m).SequenceEqual(seen);
        }));
    }

    /// ReplaceWith says "changed" exactly when the two sets of names (case ignored) differ.
    [Test]
    public void Property_ReplaceWith_ChangedIffTheNameSetsDiffer()
    {
        var pair = Gen.Two(Gen.ListOf(Props.TextFrom("abAB", 2)).Select(l => l.ToArray()));
        Props.Check(Prop.ForAll(pair.ToArbitrary(), p =>
        {
            object a = ModelOf(p.Item1.Where(n => n.Length > 0).ToArray());
            object b = ModelOf(p.Item2.Where(n => n.Length > 0).ToArray());
            var sa = new HashSet<string>(Names(a), StringComparer.OrdinalIgnoreCase);
            var sb = new HashSet<string>(Names(b), StringComparer.OrdinalIgnoreCase);
            bool changed = (bool)Call(a, "ReplaceWith", b);
            return changed == !sa.SetEquals(sb) && Count(a) == Count(b);
        }));
    }
}
