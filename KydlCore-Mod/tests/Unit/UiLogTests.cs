using System;
using System.Collections.Generic;
using System.Reflection;
using FsCheck;

namespace KydlCore.Tests.Unit;

/// <summary>
/// KydlCore.Ui.UiLog (D-071): which mod a line of the shared UI code belongs to. `Owner` is set around a mod's tab builder (Within) and every
/// control remembers it for its callbacks (Wrap). Reached in the BUILT mod DLL by reflection (like KyHub). UiLog is static state: every test
/// puts Owner and the sinks back.
/// </summary>
[TestFixture]
public class UiLogTests
{
    const string T = "KydlCore.Ui.UiLog";
    static readonly Type Log = ModAssembly.Type(T);

    [SetUp]
    public void Reset() { SetOwner(null); }

    [TearDown]
    public void Restore() { SetOwner(null); }

    static string Owner() { return (string)ModAssembly.GetStatic(T, "Owner"); }
    static void SetOwner(string value) { ModAssembly.SetStatic(T, "Owner", value); }

    static T Within<T>(string owner, Func<T> work)
    {
        try { return (T)Log.GetMethod("Within").MakeGenericMethod(typeof(T)).Invoke(null, new object[] { owner, work }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    static Action Wrap(Action a) { return (Action)ModAssembly.Call(T, "Wrap", new object[] { a }); }

    static Action<string> WrapOne(Action<string> a)
    {
        var m = Log.GetMethod("Wrap", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Action<string>) }, null);
        if (m == null) m = Array.Find(Log.GetMethods(), x => x.Name == "Wrap" && x.IsGenericMethodDefinition).MakeGenericMethod(typeof(string));
        else if (m.IsGenericMethodDefinition) m = m.MakeGenericMethod(typeof(string));
        return (Action<string>)m.Invoke(null, new object[] { a });
    }

    [Test]
    public void Owner_IsNone_Initially() { Assert.That(Owner(), Is.Null); }

    [Test]
    public void Within_SetsTheOwnerWhileRunning_AndRestoresIt()
    {
        string seen = null;
        int result = Within("com.kydldev.A", () => { seen = Owner(); return 7; });
        Assert.That(result, Is.EqualTo(7));
        Assert.That(seen, Is.EqualTo("com.kydldev.A"));
        Assert.That(Owner(), Is.Null);
    }

    [Test]
    public void Within_Nested_RestoresTheOuterOwner()
    {
        string inner = null, afterInner = null;
        Within("outer", () =>
        {
            Within("inner", () => { inner = Owner(); return 0; });
            afterInner = Owner();
            return 0;
        });
        Assert.That(inner, Is.EqualTo("inner"));
        Assert.That(afterInner, Is.EqualTo("outer"));
        Assert.That(Owner(), Is.Null);
    }

    [Test]
    public void Within_RestoresTheOwner_WhenTheWorkThrows()
    {
        SetOwner("before");
        Assert.Throws<InvalidOperationException>(() => Within<int>("x", () => { throw new InvalidOperationException("boom"); }));
        Assert.That(Owner(), Is.EqualTo("before"));
    }

    [Test]
    public void Within_NullWork_GivesDefault_AndKeepsTheOwner()
    {
        SetOwner("keep");
        Assert.That(Within<string>("other", null), Is.Null);
        Assert.That(Owner(), Is.EqualTo("keep"));
    }

    [Test]
    public void Wrap_NullIn_NullOut()
    {
        Assert.That(Wrap(null), Is.Null);
        Assert.That(WrapOne(null), Is.Null);
    }

    [Test]
    public void Wrap_RunsWithTheOwnerOfTheMomentItWasWrapped()
    {
        string seen = "never ran";
        Action wrapped = null;
        Within("com.kydldev.B", () => { wrapped = Wrap(() => seen = Owner()); return 0; });
        Assert.That(Owner(), Is.Null);
        SetOwner("someone else");
        wrapped();
        Assert.That(seen, Is.EqualTo("com.kydldev.B"));
        Assert.That(Owner(), Is.EqualTo("someone else"), "the owner of the caller is back after the callback");
    }

    [Test]
    public void Wrap_NoOwnerAtWrapTime_RunsWithNoOwner()
    {
        string seen = "never ran";
        var wrapped = Wrap(() => seen = Owner());
        SetOwner("late");
        wrapped();
        Assert.That(seen, Is.Null);
        Assert.That(Owner(), Is.EqualTo("late"));
    }

    [Test]
    public void WrapWithArgument_PassesTheArgument_AndKeepsTheOwner()
    {
        string value = null, owner = null;
        Action<string> wrapped = null;
        Within("com.kydldev.C", () => { wrapped = WrapOne(v => { value = v; owner = Owner(); }); return 0; });
        wrapped("hello");
        Assert.That(value, Is.EqualTo("hello"));
        Assert.That(owner, Is.EqualTo("com.kydldev.C"));
        Assert.That(Owner(), Is.Null);
    }

    [Test]
    public void Wrap_ARunThatThrows_StillRestoresTheOwner()
    {
        Action wrapped = null;
        Within("com.kydldev.D", () => { wrapped = Wrap(() => { throw new InvalidOperationException("click failed"); }); return 0; });
        SetOwner("now");
        Assert.Throws<InvalidOperationException>(() => wrapped());
        Assert.That(Owner(), Is.EqualTo("now"));
    }

    [Test]
    public void Lines_GoToTheSink_WithTheirText()
    {
        var lines = new List<string>();
        var saved = new[] { ModAssembly.GetStatic(T, "VerboseSink"), ModAssembly.GetStatic(T, "WarnSink"), ModAssembly.GetStatic(T, "ErrorSink") };
        try
        {
            ModAssembly.SetStatic(T, "VerboseSink", (Action<string>)(s => lines.Add("v:" + s)));
            ModAssembly.SetStatic(T, "WarnSink", (Action<string>)(s => lines.Add("w:" + s)));
            ModAssembly.SetStatic(T, "ErrorSink", (Action<string>)(s => lines.Add("e:" + s)));
            ModAssembly.Call(T, "Verbose", "one");
            ModAssembly.Call(T, "Warn", "two");
            ModAssembly.Call(T, "Error", "three");
        }
        finally
        {
            ModAssembly.SetStatic(T, "VerboseSink", saved[0]);
            ModAssembly.SetStatic(T, "WarnSink", saved[1]);
            ModAssembly.SetStatic(T, "ErrorSink", saved[2]);
        }
        Assert.That(lines, Is.EqualTo(new[] { "v:one", "w:two", "e:three" }));
    }

    [Test]
    public void Lines_WithNoSink_DoNotThrow()
    {
        var saved = ModAssembly.GetStatic(T, "VerboseSink");
        try
        {
            ModAssembly.SetStatic(T, "VerboseSink", null);
            Assert.DoesNotThrow(() => ModAssembly.Call(T, "Verbose", "nobody listens"));
        }
        finally { ModAssembly.SetStatic(T, "VerboseSink", saved); }
    }

    [Test]
    public void Property_AnyNesting_EndsWithTheOwnerWeStartedWith()
    {
        var owners = Gen.Elements("a", "b", null, "com.kydldev.X");
        Props.Check(Prop.ForAll(Gen.NonEmptyListOf(owners).ToArbitrary(), fsList =>
        {
            var list = System.Linq.Enumerable.ToArray(fsList);
            SetOwner("start");
            Func<int, int> run = null;
            run = i => i >= list.Length ? 0 : Within(list[i], () => { bool inside = Owner() == list[i]; return (inside ? 0 : 1000) + run(i + 1); });
            int wrong = run(0);
            return wrong == 0 && Owner() == "start";
        }));
    }
}
