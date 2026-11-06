using KydlCore.Features.Diagnostics.Domain;

namespace KydlCore.Tests.Unit;

/// <summary>
/// The log lines that start a flight recorder dump. The lab's lines are copied from a real session (SteveTheTroll 20261105-111953).
/// </summary>
[TestFixture]
public class TriggerRulesTests
{
    [Test]
    public void LabFailedLine_GivesTestAndReason()
    {
        bool ok = TriggerRules.TryLabResult("DrakesTestLab",
            "Test SteveTheTroll/unarmed-farm: Failed - step 16/16: do=wait, until={check=hook, name=stt.plantedUntouched, expect=0}, timeout=45 failed: waited",
            out string test, out bool failed, out string why);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ok, Is.True);
            Assert.That(test, Is.EqualTo("SteveTheTroll/unarmed-farm"));
            Assert.That(failed, Is.True);
            Assert.That(why, Does.StartWith("step 16/16: do=wait"));
        }
    }

    [Test]
    public void LabPassedLine_IsAResultThatDidNotFail()
    {
        bool ok = TriggerRules.TryLabResult("DrakesTestLab", "Test SteveTheTroll/farm-chops-tree: Passed", out string test, out bool failed, out string why);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ok, Is.True);
            Assert.That(test, Is.EqualTo("SteveTheTroll/farm-chops-tree"));
            Assert.That(failed, Is.False);
            Assert.That(why, Is.Empty);
        }
    }

    [TestCase("SteveTheTroll", "Test SteveTheTroll/x: Failed - why", Description = "another source writing the same words")]
    [TestCase("DrakesTestLab", "Test state reset.")]
    [TestCase("DrakesTestLab", "Test hooks found: testlab.echo, testlab.keyHeld")]
    [TestCase("DrakesTestLab", "Test arena ready at (5.63, 53.02, 4.24) (radius 40).")]
    [TestCase("DrakesTestLab", "")]
    [TestCase("DrakesTestLab", null)]
    public void OtherLines_AreNoResult(string source, string text)
    {
        Assert.That(TriggerRules.TryLabResult(source, text, out _, out _, out _), Is.False);
    }

    [Test]
    public void LabReset_IsRecognised_OnlyFromTheLab()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TriggerRules.IsLabReset("DrakesTestLab", "Test state reset."), Is.True);
            Assert.That(TriggerRules.IsLabReset("DrakesTestLab", "Test state reset.\r\n"), Is.True);
            Assert.That(TriggerRules.IsLabReset("SteveTheTroll", "Test state reset."), Is.False);
        }
    }

    [TestCase("TestLab failed: STT/unarmed-farm - step 16", "testlab-failed-stt-unarmed-farm-step-16")]
    [TestCase("CHECK OFF in WhipItUp: angle = 12 deg", "check-off-in-whipitup-angle-12-deg")]
    [TestCase("!!!", "dump")]
    [TestCase(null, "dump")]
    [TestCase("Ärger", "rger")]
    public void Slug_IsSafeForAFileName(string reason, string expected)
    {
        Assert.That(TriggerRules.Slug(reason), Is.EqualTo(expected));
    }

    [Test]
    public void Slug_IsCutToTheLimit()
    {
        Assert.That(TriggerRules.Slug(new string('a', 100), 40), Has.Length.EqualTo(40));
    }

    [Test]
    public void Short_KeepsTheFirstLineAndCuts()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TriggerRules.Short("line one\r\nline two", 100), Is.EqualTo("line one"));
            Assert.That(TriggerRules.Short("abcdef", 3), Is.EqualTo("abc..."));
            Assert.That(TriggerRules.Short(null, 3), Is.Empty);
        }
    }
}
