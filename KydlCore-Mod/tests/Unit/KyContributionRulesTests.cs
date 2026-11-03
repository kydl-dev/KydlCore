using Kydl.Core.Results;
using KydlCore.Features.Items.Domain;

namespace KydlCore.Tests.Unit
{
    /// Spec 5 / MIGRATION_DECISIONS B13: the id rules and the duplicate book of KY-Items contributions (pure, linked from src/Features/Items/Domain).
    public class KyContributionRulesTests
    {
        const string Steve = "com.kydldev.SteveTheTroll";

        [TestCase("stevethetroll:slippers", "stevethetroll:slippers")]
        [TestCase("SteveTheTroll:Troll_Hub", "stevethetroll:troll_hub")]
        [TestCase("  stevethetroll:club-2.v1 ", "stevethetroll:club-2.v1")]
        public void ValidId_IsAcceptedInLowercase(string id, string expected)
        {
            var r = KyItemRules.ValidateId(id, Steve);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Success)); Assert.That(r.Value, Is.EqualTo(expected));
        }

        [TestCase(null, Steve, KyItemRules.NoId)]
        [TestCase("", Steve, KyItemRules.NoId)]
        [TestCase("stevethetroll:x", null, KyItemRules.NoMod)]
        [TestCase("other:x", "com.other.Mod", KyItemRules.NotUserMod)]
        [TestCase("com.kydldev.:x", "com.kydldev.", KyItemRules.NotUserMod)]
        [TestCase("slippers", Steve, KyItemRules.BadShape)]
        [TestCase(":slippers", Steve, KyItemRules.BadShape)]
        [TestCase("stevethetroll:", Steve, KyItemRules.BadShape)]
        [TestCase("stevethetroll:a:b", Steve, KyItemRules.BadShape)]
        [TestCase("stevethetroll:troll hub", Steve, KyItemRules.BadChars)]
        [TestCase("stevethetroll:trollé", Steve, KyItemRules.BadChars)]
        public void InvalidId_IsRejectedWithTheReason(string id, string guid, string reason)
        {
            var r = KyItemRules.ValidateId(id, guid);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo(reason));
        }

        [Test] public void ForeignNamespace_IsRejected()
        {
            var r = KyItemRules.ValidateId("kydltools:slippers", Steve);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Does.Contain("'kydltools'").And.Contain("'stevethetroll'"));
        }

        [TestCase("com.kydldev.SteveTheTroll", "stevethetroll")]
        [TestCase("COM.KYDLDEV.KydlCore", "kydlcore")]
        [TestCase("com.other.X", "")]
        [TestCase(null, "")]
        public void ModId(string guid, string expected) { Assert.That(KyItemRules.ModId(guid), Is.EqualTo(expected)); }

        [Test] public void Book_RejectsDuplicateIdsAndPrefabs_AcrossMods()
        {
            var b = new ContributionBook();
            Assert.That(b.Add(new ContributionRecord("stevethetroll:slippers", Steve, "Slippers")).Value, Is.EqualTo(1));
            var dupId = b.Add(new ContributionRecord("stevethetroll:slippers", "com.kydldev.Other", "Other"));
            Assert.That(dupId.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(dupId.Reason, Does.Contain(Steve));
            var dupPrefab = b.Add(new ContributionRecord("other:slippers", "com.kydldev.Other", "SLIPPERS"));
            Assert.That(dupPrefab.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(dupPrefab.Reason, Does.Contain("stevethetroll:slippers"));
            Assert.That(b.Add(new ContributionRecord("other:x", "com.kydldev.Other", "")).Reason, Is.EqualTo(ContributionBook.NoPrefab));
            Assert.That(b.Count, Is.EqualTo(1));
        }

        [Test] public void Book_Remove_FreesIdAndPrefab()
        {
            var b = new ContributionBook();
            b.Add(new ContributionRecord("kydlcore:a", "com.kydldev.KydlCore", "Wood"));
            Assert.That(b.Find("kydlcore:a").Prefab, Is.EqualTo("Wood"));
            Assert.That(b.Remove("kydlcore:a"), Is.True); Assert.That(b.Remove("never"), Is.True); Assert.That(b.Remove(null), Is.True);
            Assert.That(b.Contains("kydlcore:a"), Is.False); Assert.That(b.Find("kydlcore:a").Prefab, Is.EqualTo(""));
            Assert.That(b.Add(new ContributionRecord("kydlcore:b", "com.kydldev.KydlCore", "Wood")).IsSuccess, Is.True);
        }
    }
}
