using KydlCore.Features.Diagnostics.Domain;

namespace KydlCore.Tests.Unit;

/// <summary>When a flight recorder dump may be written: one per kind within its gap, a fixed number per game start.</summary>
[TestFixture]
public class DumpGateTests
{
    [Test]
    public void SameKindWithinTheGap_IsRefused_AfterTheGap_Allowed()
    {
        var gate = new DumpGate();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gate.Allow("check-off:STT", 0, 60, 30), Is.True);
            Assert.That(gate.Allow("check-off:STT", 59, 60, 30), Is.False);
            Assert.That(gate.Allow("check-off:STT", 60, 60, 30), Is.True);
            Assert.That(gate.Written, Is.EqualTo(2));
            Assert.That(gate.Refused, Is.EqualTo(1));
        }
    }

    [Test]
    public void OtherKinds_HaveTheirOwnGap()
    {
        var gate = new DumpGate();
        gate.Allow("check-off:STT", 0, 60, 30);

        Assert.That(gate.Allow("check-off:WhipItUp", 1, 60, 30), Is.True);
    }

    [Test]
    public void ZeroGap_EveryFailedTestGetsItsDump()
    {
        var gate = new DumpGate();

        Assert.That(gate.Allow("testlab", 0, 0, 30) && gate.Allow("testlab", 0, 0, 30), Is.True);
    }

    [Test]
    public void TheLimitPerGameStart_IsKept()
    {
        var gate = new DumpGate();
        for (int i = 0; i < 3; i++) gate.Allow("testlab", i, 0, 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gate.Allow("testlab", 10, 0, 3), Is.False);
            Assert.That(gate.Written, Is.EqualTo(3));
        }
    }

    [Test]
    public void NullKind_CountsAsOneKind()
    {
        var gate = new DumpGate();
        gate.Allow(null, 0, 60, 30);

        Assert.That(gate.Allow(null, 1, 60, 30), Is.False);
    }
}
