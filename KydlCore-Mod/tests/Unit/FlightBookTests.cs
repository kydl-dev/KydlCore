using System;
using System.Text.RegularExpressions;
using KydlCore.Features.Diagnostics.Domain;

namespace KydlCore.Tests.Unit;

/// <summary>
/// The flight recorder's memory and the text of a dump: tracked keys, samples per key, gone keys, the window, the timeline, wall-clock times.
/// </summary>
[TestFixture]
public class FlightBookTests
{
    static readonly DateTime Wall = new DateTime(2026, 11, 5, 11, 32, 21, 500);

    static FlightBook Book(int samples = 10) => new FlightBook(samples, 50, samples);

    [Test]
    public void Start_NewKey_IsTracked_EmptyKeyIsNot()
    {
        var book = Book();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(book.Start("zdo 1:2", "SteveTheTroll", "Spencer 1", 0), Is.True);
            Assert.That(book.Start("", "SteveTheTroll", "x", 0), Is.False);
            Assert.That(book.TrackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Add_UntrackedKey_IsRefused()
    {
        Assert.That(Book().Add("zdo 9:9", new FlightSample(1, 0, 0, 0, 0, "")), Is.False);
    }

    [Test]
    public void Start_SameKeyAgain_KeepsHistoryAndRenames()
    {
        var book = Book();
        book.Start("k", "M", "old name", 0);
        book.Add("k", new FlightSample(1, 1, 2, 3, 0, "a"));
        book.Start("k", "M", "new name", 2);

        string dump = book.Format("why", 2, 60, Wall, 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dump, Does.Contain("== M / new name (k)"));
            Assert.That(dump, Does.Contain("a"));
            Assert.That(book.TrackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Stop_RemovesTheKey()
    {
        var book = Book();
        book.Start("k", "M", "x", 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(book.Stop("k"), Is.True);
            Assert.That(book.Stop("k"), Is.False);
            Assert.That(book.IsTracked("k"), Is.False);
        }
    }

    [Test]
    public void Prune_DropsOnlyKeysGoneLongerThanTheLimit()
    {
        var book = Book();
        book.Start("old", "M", "a", 0);
        book.Start("new", "M", "b", 0);
        book.Start("alive", "M", "c", 0);
        book.MarkGone("old", 10);
        book.MarkGone("new", 100);

        int dropped = book.Prune(120, 60);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dropped, Is.EqualTo(1));
            Assert.That(book.IsTracked("old"), Is.False);
            Assert.That(book.IsTracked("new"), Is.True);
            Assert.That(book.IsTracked("alive"), Is.True);
        }
    }

    [Test]
    public void Add_AfterGone_ClearsTheGoneMark()
    {
        var book = Book();
        book.Start("k", "M", "x", 0);
        book.MarkGone("k", 5);
        book.Add("k", new FlightSample(6, 0, 0, 0, 0, "back"));

        Assert.That(book.Prune(1000, 1), Is.Zero, "a key that came back is not gone");
    }

    [Test]
    public void Format_HoldsReasonEnvironmentTimelineAndSamples()
    {
        var book = Book();
        book.Start("zdo 1:2", "SteveTheTroll", "Spencer 1", 0);
        book.Env(new FlightNote(9, "env", "fps 78 | sim near 2 total 4"));
        book.Note(new FlightNote(9.5, "TestLab", "test SteveTheTroll/unarmed-farm: FAILED"));
        book.Add("zdo 1:2", new FlightSample(10, -33.4, 52, 81, 1.5, "target Greyling"));

        string dump = book.Format("TestLab failed: unarmed-farm", 10, 120, Wall, 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dump, Does.StartWith("FLIGHT RECORDER DUMP - TestLab failed: unarmed-farm"));
            Assert.That(dump, Does.Contain("fps 78 | sim near 2 total 4"));
            Assert.That(dump, Does.Contain("[TestLab] test SteveTheTroll/unarmed-farm: FAILED"));
            Assert.That(dump, Does.Contain("== SteveTheTroll / Spencer 1 (zdo 1:2)"));
            Assert.That(dump, Does.Match(@"11:32:21\.500\s+-33\.4\s+52\.0\s+81\.0\s+1\.50  target Greyling"));
        }
    }

    [Test]
    public void Format_TimesAreWallClockRelativeToNow()
    {
        var book = Book();
        book.Env(new FlightNote(7.5, "env", "e"));

        string dump = book.Format("r", 10, 120, Wall, 10);

        Assert.That(dump, Does.Contain("11:32:19.000  e"), "2.5 s before now = 2.5 s before the wall time");
    }

    [Test]
    public void Format_LeavesOutWhatIsOlderThanTheWindow()
    {
        var book = Book();
        book.Start("k", "M", "x", 0);
        book.Add("k", new FlightSample(1, 0, 0, 0, 0, "ancient"));
        book.Add("k", new FlightSample(95, 0, 0, 0, 0, "recent"));
        book.Note(new FlightNote(2, "M", "old note"));

        string dump = book.Format("r", 100, 30, Wall, 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dump, Does.Not.Contain("ancient"));
            Assert.That(dump, Does.Not.Contain("old note"));
            Assert.That(dump, Does.Contain("recent"));
        }
    }

    [Test]
    public void Format_UnchangedFactsAreWrittenAsSame()
    {
        var book = Book();
        book.Start("k", "M", "x", 0);
        book.Add("k", new FlightSample(1, 0, 0, 0, 0, "idle"));
        book.Add("k", new FlightSample(2, 0, 0, 0, 0, "idle"));

        string dump = book.Format("r", 2, 60, Wall, 10);

        Assert.That(Regex.Matches(dump, "idle").Count, Is.EqualTo(1));
        Assert.That(dump, Does.Contain("  same"));
    }

    [Test]
    public void Format_GoneKey_IsMarked()
    {
        var book = Book();
        book.Start("k", "M", "x", 0);
        book.MarkGone("k", 4);

        Assert.That(book.Format("r", 5, 60, Wall, 10), Does.Contain("GONE since 11:32:20.500"));
    }

    [Test]
    public void Format_MoreKeysThanTheLimit_WritesTheNewestAndCountsTheRest()
    {
        var book = Book();
        for (int i = 0; i < 5; i++)
        {
            book.Start("k" + i, "M", "obj" + i, 0);
            book.Add("k" + i, new FlightSample(i, 0, 0, 0, 0, ""));
        }

        string dump = book.Format("r", 5, 60, Wall, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dump, Does.Contain("(k4)"));
            Assert.That(dump, Does.Contain("(k3)"));
            Assert.That(dump, Does.Not.Contain("(k0)"));
            Assert.That(dump, Does.Contain("(3 more tracked object(s) not written)"));
        }
    }

    [Test]
    public void SamplesPerTrack_KeepsOnlyTheNewest()
    {
        var book = Book(2);
        book.Start("k", "M", "x", 0);
        book.Add("k", new FlightSample(1, 0, 0, 0, 0, "first"));
        book.Add("k", new FlightSample(2, 0, 0, 0, 0, "second"));
        book.Add("k", new FlightSample(3, 0, 0, 0, 0, "third"));

        string dump = book.Format("r", 3, 60, Wall, 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dump, Does.Not.Contain("first"));
            Assert.That(dump, Does.Contain("second"));
            Assert.That(dump, Does.Contain("third"));
        }
    }
}
