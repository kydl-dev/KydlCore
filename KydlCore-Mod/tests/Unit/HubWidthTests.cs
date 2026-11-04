
using System.Reflection;

namespace KydlCore.Tests.Unit;

/// <summary>
/// The KY hub is 15% wider than the shared 760 px window (user, 2026-10-30): 10% of the old width further right, 5% further left.
/// Reached in the BUILT mod DLL by reflection (HubView is internal).
/// </summary>
[TestFixture]
public class HubWidthTests
{
    const string T = "KydlCore.Ui.HubView";
    const float Old = 760f;

    static float Const(string name)
    {
        var f = ModAssembly.Type(T).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(f, Is.Not.Null, name);
        return (float)f.GetRawConstantValue();
    }

    static float Right(float screen)
    {
        var m = ModAssembly.Type(T).GetMethod("RightOffset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return (float)m.Invoke(null, new object[] { screen });
    }

    [Test]
    public void Width_IsFifteenPercentMoreThanTheSharedWindow()
    {
        Assert.That(Const("WindowWidth"), Is.EqualTo(Old * 1.15f).Within(0.5f));
    }

    [Test]
    public void RightSide_MovesTenPercentOfTheOldWidthTowardsTheEdge()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Const("ShiftRight"), Is.EqualTo(Old * 0.10f).Within(0.5f));
            // 3440 px wide: the shared window keeps 5% = 172 px from the edge, the hub 172 - 76
            Assert.That(Right(3440f), Is.EqualTo(3440f * 0.05f - 76f).Within(0.01f));
        }
    }

    [Test]
    public void LeftSide_GrowsFivePercentOfTheOldWidth()
    {
        // left edge = screen - right - width; shared window: screen - 0.05 screen - 760
        float screen = 3440f;
        float oldLeft = screen - screen * 0.05f - Old;
        float newLeft = screen - Right(screen) - Const("WindowWidth");

        Assert.That(oldLeft - newLeft, Is.EqualTo(Old * 0.05f).Within(0.5f));
    }

    [TestCase(1000f)]
    [TestCase(1280f)]
    [TestCase(0f)]
    public void RightSide_NeverGoesOffTheScreen(float screen)
    {
        Assert.That(Right(screen), Is.GreaterThanOrEqualTo(0f));
    }
}
