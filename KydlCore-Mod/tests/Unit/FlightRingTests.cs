using System;
using System.Linq;
using FsCheck;
using KydlCore.Features.Diagnostics.Domain;

namespace KydlCore.Tests.Unit;

/// <summary>The flight recorder's ring: keeps only the newest items, hands them out oldest first.</summary>
[TestFixture]
public class FlightRingTests
{
    [Test]
    public void New_CapacityBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FlightRing<int>(0));
    }

    [Test]
    public void Add_MoreThanCapacity_KeepsTheNewestOldestFirst()
    {
        var ring = new FlightRing<int>(3);
        foreach (int i in Enumerable.Range(1, 5)) ring.Add(i);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ring.Count, Is.EqualTo(3));
            Assert.That(ring.Items(), Is.EqualTo(new[] { 3, 4, 5 }));
            Assert.That(ring.Newest, Is.EqualTo(5));
        }
    }

    [Test]
    public void Items_WithFilter_KeepsOrderAndSkipsTheRest()
    {
        var ring = new FlightRing<int>(10);
        foreach (int i in Enumerable.Range(1, 6)) ring.Add(i);

        Assert.That(ring.Items(x => x % 2 == 0), Is.EqualTo(new[] { 2, 4, 6 }));
    }

    [Test]
    public void Empty_NewestIsDefault_ItemsEmpty()
    {
        var ring = new FlightRing<string>(2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ring.Newest, Is.Null);
            Assert.That(ring.Items(), Is.Empty);
        }
    }

    [Test]
    public void Items_IsACopy()
    {
        var ring = new FlightRing<int>(2);
        ring.Add(1);
        ring.Items().Add(99);

        Assert.That(ring.Items(), Is.EqualTo(new[] { 1 }));
    }

    /// Rule: whatever is added, the ring holds min(added, capacity) items and they are exactly the last ones added, in order.
    [Test]
    public void Property_HoldsExactlyTheLastItemsInOrder()
    {
        Props.Check(Prop.ForAll(Gen.Choose(1, 20).ToArbitrary(), Gen.Choose(0, 60).ToArbitrary(), (cap, n) =>
        {
            var ring = new FlightRing<int>(cap);
            for (int i = 0; i < n; i++) ring.Add(i);
            var expected = Enumerable.Range(0, n).Skip(Math.Max(0, n - cap)).ToArray();
            return ring.Count == Math.Min(n, cap) && ring.Items().SequenceEqual(expected);
        }));
    }
}
