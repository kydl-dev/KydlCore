using FsCheck;

namespace KydlCore.Tests.Unit
{
    /// <summary>The rules for the UI log lines of a mod (D-071): whose debug switch decides, and the "[Mod] text" tag.</summary>
    [TestFixture]
    public class UiLogRouteTests
    {
        const string T = "KydlCore.Features.Hub.Domain.UiLogRoute";

        static bool Show(string owner, bool ownerOn, bool coreOn) { return (bool)ModAssembly.Call(T, "ShowVerbose", owner, ownerOn, coreOn); }
        static string Tag(string name, string message) { return (string)ModAssembly.Call(T, "Tag", name, message); }

        [TestCase("com.kydldev.A", true, false, ExpectedResult = true, TestName = "mod on, core off: shown (the mod's switch decides)")]
        [TestCase("com.kydldev.A", false, true, ExpectedResult = false, TestName = "mod off, core on: hidden (not the core's switch)")]
        [TestCase("com.kydldev.A", true, true, ExpectedResult = true)]
        [TestCase("com.kydldev.A", false, false, ExpectedResult = false)]
        [TestCase(null, false, true, ExpectedResult = true, TestName = "no owner, core on: shown")]
        [TestCase(null, true, false, ExpectedResult = false, TestName = "no owner, core off: hidden (a mod's switch does not matter)")]
        public bool ShowVerbose_FollowsTheOwnersSwitch_OrCoresWithoutOwner(string owner, bool ownerOn, bool coreOn) { return Show(owner, ownerOn, coreOn); }

        [Test]
        public void Tag_PutsTheModNameInSquareBrackets_InFront()
        {
            Assert.That(Tag("ChestDecor", "Ui: button 'Save' pressed"), Is.EqualTo("[ChestDecor] Ui: button 'Save' pressed"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Tag_WithNoName_LeavesTheMessageAlone(string name)
        {
            Assert.That(Tag(name, "plain"), Is.EqualTo("plain"));
        }

        [Test]
        public void Property_TaggedLine_StartsWithTheNameAndEndsWithTheMessage()
        {
            var names = Props.TextFrom("abcXYZ019_", 12);
            var messages = Props.TextFrom("abc :'[]xyz", 30);
            Props.Check(Prop.ForAll(names.ToArbitrary(), messages.ToArbitrary(), (n, m) =>
            {
                string line = Tag(n, m);
                return line == "[" + n + "] " + m && line.StartsWith("[" + n + "]") && line.EndsWith(m);
            }));
        }

        [Test]
        public void Property_NoOwner_NeverDependsOnTheOwnerSwitch()
        {
            Props.Check(Prop.ForAll(Arb.Default.Bool(), Arb.Default.Bool(), (ownerOn, coreOn) => Show(null, ownerOn, coreOn) == coreOn));
        }
    }
}
