using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FsCheck;

namespace KydlCore.Tests.Unit
{
    /// <summary>
    /// WindowStack (PLAN T-021, DESIGN 4): open pushes, an id is in the stack once, a click brings a window to the front, Escape closes only the
    /// top, Close removes one, and AnyOpenChanged fires only when the first window opens / the last one closes (the input lock follows it).
    /// The property test runs random command sequences against a plain List model. The class under test is reached in the BUILT mod DLL by
    /// reflection (like Tunable): that is what lets Stryker mutate it (test-quality.ps1).
    /// </summary>
    [TestFixture]
    public class WindowStackTests
    {
        /// <summary>The mod's internal KydlCore.Features.Hub.Domain.WindowStack, called through reflection; same members, exceptions are not wrapped.</summary>
        sealed class WindowStack
        {
            static readonly Type T = ModAssembly.Type("KydlCore.Features.Hub.Domain.WindowStack");
            readonly object _o = Activator.CreateInstance(T);

            public int Count { get { return (int)T.GetProperty("Count").GetValue(_o); } }
            public bool AnyOpen { get { return (bool)T.GetProperty("AnyOpen").GetValue(_o); } }
            public string Top { get { return (string)T.GetProperty("Top").GetValue(_o); } }
            public bool IsOpen(string id) { return (bool)Call("IsOpen", id); }
            public string[] Ids() { return (string[])Call("Ids"); }
            public bool Open(string id) { return (bool)Call("Open", id); }
            public bool BringToFront(string id) { return (bool)Call("BringToFront", id); }
            public bool Close(string id) { return (bool)Call("Close", id); }
            public string CloseTop() { return (string)Call("CloseTop"); }
            public event Action<bool> AnyOpenChanged
            {
                add { T.GetEvent("AnyOpenChanged").AddEventHandler(_o, value); }
                remove { T.GetEvent("AnyOpenChanged").RemoveEventHandler(_o, value); }
            }

            object Call(string name, params object[] args)
            {
                try { return T.GetMethod(name).Invoke(_o, args); }
                catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
            }
        }

        sealed class Watched
        {
            public readonly WindowStack Stack = new WindowStack();
            public readonly List<bool> Events = new List<bool>();
            public Watched() { Stack.AnyOpenChanged += open => Events.Add(open); }
            public Watched With(params string[] ids) { foreach (var id in ids) Stack.Open(id); Events.Clear(); return this; }
        }

        static string Join(WindowStack s) { return string.Join(",", s.Ids()); }

        [Test]
        public void New_IsEmpty()
        {
            var s = new WindowStack();
            Assert.That(s.Count, Is.EqualTo(0));
            Assert.That(s.AnyOpen, Is.False);
            Assert.That(s.Top, Is.Null);
            Assert.That(s.Ids(), Is.Empty);
        }

        [Test]
        public void Open_PutsTheNewWindowOnTop_InOrder()
        {
            var w = new Watched();
            Assert.That(w.Stack.Open("a"), Is.True);
            Assert.That(w.Stack.Open("b"), Is.True);
            Assert.That(w.Stack.Open("c"), Is.True);
            Assert.That(Join(w.Stack), Is.EqualTo("a,b,c"));
            Assert.That(w.Stack.Top, Is.EqualTo("c"));
            Assert.That(w.Stack.Count, Is.EqualTo(3));
        }

        [Test]
        public void Open_AlreadyOpen_OnlyBringsItToTheFront_NoSecondEntry_NoEvent()
        {
            var w = new Watched().With("a", "b", "c");
            Assert.That(w.Stack.Open("a"), Is.True);
            Assert.That(Join(w.Stack), Is.EqualTo("b,c,a"));
            Assert.That(w.Stack.Count, Is.EqualTo(3));
            Assert.That(w.Events, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Open_InvalidId_ReturnsFalse_AndChangesNothing(string id)
        {
            var w = new Watched().With("a");
            Assert.That(w.Stack.Open(id), Is.False);
            Assert.That(Join(w.Stack), Is.EqualTo("a"));
            Assert.That(w.Events, Is.Empty);
            var empty = new Watched();
            Assert.That(empty.Stack.Open(id), Is.False);
            Assert.That(empty.Stack.AnyOpen, Is.False);
            Assert.That(empty.Events, Is.Empty);
        }

        [Test]
        public void Ids_AreComparedOrdinally_CaseSensitive()
        {
            var w = new Watched().With("Hub");
            w.Stack.Open("hub");
            Assert.That(Join(w.Stack), Is.EqualTo("Hub,hub"));
            Assert.That(w.Stack.IsOpen("HUB"), Is.False);
        }

        [Test]
        public void IsOpen_TellsOpenFromClosed_AndNeverForInvalidIds()
        {
            var w = new Watched().With("a");
            Assert.That(w.Stack.IsOpen("a"), Is.True);
            Assert.That(w.Stack.IsOpen("b"), Is.False);
            Assert.That(w.Stack.IsOpen(null), Is.False);
            Assert.That(w.Stack.IsOpen(""), Is.False);
        }

        [Test]
        public void BringToFront_MovesAnOpenWindowToTheTop_AndKeepsTheOthersInOrder()
        {
            var w = new Watched().With("a", "b", "c", "d");
            Assert.That(w.Stack.BringToFront("b"), Is.True);
            Assert.That(Join(w.Stack), Is.EqualTo("a,c,d,b"));
            Assert.That(w.Events, Is.Empty);
        }

        [Test]
        public void BringToFront_OfTheTopWindow_ChangesNothing()
        {
            var w = new Watched().With("a", "b");
            Assert.That(w.Stack.BringToFront("b"), Is.True);
            Assert.That(Join(w.Stack), Is.EqualTo("a,b"));
        }

        [TestCase("zzz")]
        [TestCase(null)]
        [TestCase("")]
        public void BringToFront_OfAWindowThatIsNotOpen_ReturnsFalse_AndDoesNotOpenIt(string id)
        {
            var w = new Watched().With("a");
            Assert.That(w.Stack.BringToFront(id), Is.False);
            Assert.That(Join(w.Stack), Is.EqualTo("a"));
        }

        [Test]
        public void Close_RemovesTheWindowWhereverItIs_AndKeepsTheOrderOfTheRest()
        {
            var w = new Watched().With("a", "b", "c");
            Assert.That(w.Stack.Close("b"), Is.True);
            Assert.That(Join(w.Stack), Is.EqualTo("a,c"));
            Assert.That(w.Stack.IsOpen("b"), Is.False);
            Assert.That(w.Events, Is.Empty);
        }

        [TestCase("zzz")]
        [TestCase(null)]
        [TestCase("")]
        public void Close_OfAWindowThatIsNotOpen_ReturnsFalse_AndChangesNothing(string id)
        {
            var w = new Watched().With("a");
            Assert.That(w.Stack.Close(id), Is.False);
            Assert.That(Join(w.Stack), Is.EqualTo("a"));
            Assert.That(w.Events, Is.Empty);
        }

        [Test]
        public void CloseTop_ClosesOnlyTheFrontWindow_EachEscapeOne()
        {
            var w = new Watched().With("a", "b", "c");
            Assert.That(w.Stack.CloseTop(), Is.EqualTo("c"));
            Assert.That(Join(w.Stack), Is.EqualTo("a,b"));
            Assert.That(w.Stack.CloseTop(), Is.EqualTo("b"));
            Assert.That(Join(w.Stack), Is.EqualTo("a"));
        }

        [Test]
        public void CloseTop_AfterAClick_ClosesTheClickedWindow()
        {
            var w = new Watched().With("a", "b", "c");
            w.Stack.BringToFront("a");
            Assert.That(w.Stack.CloseTop(), Is.EqualTo("a"));
            Assert.That(Join(w.Stack), Is.EqualTo("b,c"));
        }

        [Test]
        public void CloseTop_WhenNothingIsOpen_ReturnsNull_NoEvent()
        {
            var w = new Watched();
            Assert.That(w.Stack.CloseTop(), Is.Null);
            Assert.That(w.Events, Is.Empty);
            Assert.That(w.Stack.AnyOpen, Is.False);
        }

        [Test]
        public void AnyOpenChanged_FiresTrueOnTheFirstWindow_FalseOnTheLast_NothingInBetween()
        {
            var w = new Watched();
            w.Stack.Open("a");
            Assert.That(w.Events, Is.EqualTo(new[] { true }));
            w.Stack.Open("b");
            w.Stack.BringToFront("a");
            w.Stack.Open("b");
            w.Stack.Close("b");
            Assert.That(w.Events, Is.EqualTo(new[] { true }), "no event while at least one window stays open");
            w.Stack.CloseTop();
            Assert.That(w.Events, Is.EqualTo(new[] { true, false }));
            Assert.That(w.Stack.AnyOpen, Is.False);
        }

        [Test]
        public void AnyOpenChanged_FiresAgainWhenTheStackIsUsedAgain()
        {
            var w = new Watched();
            w.Stack.Open("a"); w.Stack.Close("a"); w.Stack.Open("b"); w.Stack.Close("b");
            Assert.That(w.Events, Is.EqualTo(new[] { true, false, true, false }));
        }

        [Test]
        public void Works_WithoutAnyAnyOpenChangedSubscriber()
        {
            var s = new WindowStack();
            Assert.DoesNotThrow(() => { s.Open("a"); s.Close("a"); });
        }

        [Test]
        public void Ids_IsACopy_ChangingItChangesNothing()
        {
            var w = new Watched().With("a", "b");
            var copy = w.Stack.Ids();
            copy[0] = "x";
            Array.Resize(ref copy, 0);
            Assert.That(Join(w.Stack), Is.EqualTo("a,b"));
        }

        [Test]
        public void AHandlerThatThrows_ReachesTheCaller_AndTheStackKeepsItsNewState()
        {
            var s = new WindowStack();
            s.AnyOpenChanged += open => { throw new InvalidOperationException("handler"); };
            Assert.Throws<InvalidOperationException>(() => s.Open("a"));
            Assert.That(s.IsOpen("a"), Is.True);
            Assert.That(s.Count, Is.EqualTo(1));
        }

        /// Commands: op 0 Open, 1 BringToFront, 2 Close, 3 CloseTop; id 0..3 (a small set so that ids repeat and every case is reached).
        [Test]
        public void Property_RandomCommands_BehaveLikeAPlainListModel_AndEventsFollowTheTransitions()
        {
            var commands = Gen.ArrayOf(Gen.Choose(0, 15));
            Props.Check(Prop.ForAll(commands.ToArbitrary(), cmds =>
            {
                var s = new WindowStack();
                var events = new List<bool>();
                s.AnyOpenChanged += open => events.Add(open);
                var model = new List<string>();
                var expectedEvents = new List<bool>();
                foreach (int c in cmds)
                {
                    int op = c / 4;
                    string id = "w" + (c % 4);
                    bool wasOpen = model.Count > 0;
                    bool ok = true;
                    if (op == 0)
                    {
                        model.Remove(id); model.Add(id);
                        ok = s.Open(id);
                    }
                    else if (op == 1)
                    {
                        bool contains = model.Contains(id);
                        if (contains) { model.Remove(id); model.Add(id); }
                        ok = s.BringToFront(id) == contains;
                    }
                    else if (op == 2)
                    {
                        bool contains = model.Remove(id);
                        ok = s.Close(id) == contains;
                    }
                    else
                    {
                        string expectedTop = model.Count == 0 ? null : model[model.Count - 1];
                        if (expectedTop != null) model.RemoveAt(model.Count - 1);
                        ok = s.CloseTop() == expectedTop;
                    }
                    if (wasOpen != (model.Count > 0)) expectedEvents.Add(model.Count > 0);
                    bool same = s.Ids().SequenceEqual(model) && s.Count == model.Count && s.AnyOpen == (model.Count > 0)
                                && s.Top == (model.Count == 0 ? null : model[model.Count - 1])
                                && s.Ids().Distinct().Count() == s.Count;
                    if (!ok || !same) return false;
                }
                return events.SequenceEqual(expectedEvents);
            }));
        }
    }
}
