using FsCheck;

namespace KydlCore.Tests.Unit;

/// <summary>KyWindowId (PLAN T-038): "guid:id" keys of the one window stack, reached through the built DLL.</summary>
[TestFixture]
public class KyWindowIdTests
{
    const string Type = "KydlCore.Features.Hub.Domain.KyWindowId";

    static string Make(string guid, string id) { return (string)ModAssembly.Call(Type, "Make", guid, id); }

    [Test]
    public void Make_JoinsGuidAndId() { Assert.That(Make("com.kydldev.chestdecor", "editor"), Is.EqualTo("com.kydldev.chestdecor:editor")); }

    [Test]
    public void Make_TrimsBothParts() { Assert.That(Make(" a ", " b "), Is.EqualTo("a:b")); }

    [TestCase(null, "x")]
    [TestCase("", "x")]
    [TestCase("  ", "x")]
    [TestCase("g", null)]
    [TestCase("g", "")]
    [TestCase("g", "   ")]
    public void Make_BlankPart_ReturnsNull(string guid, string id) { Assert.That(Make(guid, id), Is.Null); }

    [Test]
    public void Make_TwoModsSameId_AreDifferentWindows() { Assert.That(Make("a", "editor"), Is.Not.EqualTo(Make("b", "editor"))); }

    [Test]
    public void Make_NeverEqualsTheHubId()
    {
        Props.Check(Prop.ForAll(Arb.From<NonEmptyString>(), Arb.From<NonEmptyString>(), (g, i) =>
        {
            string full = Make(g.Get, i.Get);
            return full == null || full != (string)ModAssembly.GetStatic("KydlCore.Features.Hub.Domain.KyWindowId", "Hub");
        }));
    }
}
