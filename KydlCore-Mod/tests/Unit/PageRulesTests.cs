using System;
using NUnit.Framework;

namespace KydlCore.Tests.Unit
{
    /// <summary>T-098: the page host replaces its content only when the page's State changed (no flicker, an open drop-down stays open).</summary>
    [TestFixture]
    public class PageRulesTests
    {
        static readonly Type Rules = ModAssembly.Type("KydlCore.Features.Hub.Domain.PageRules");
        static bool Needs(string shown, string next) { return (bool)Rules.GetMethod("NeedsRebuild", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(null, new object[] { shown, next }); }

        [Test] public void FirstBuild_IsShown() { Assert.That(Needs(null, "a"), Is.True); }
        [Test] public void SameState_IsNotRebuilt() { Assert.That(Needs("a", "a"), Is.False); }
        [Test] public void ChangedState_IsRebuilt() { Assert.That(Needs("a", "b"), Is.True); }
        [Test] public void EmptyState_IsAlwaysRebuilt() { Assert.That(Needs("", ""), Is.True); }
        [Test] public void NullState_IsAlwaysRebuilt() { Assert.That(Needs("a", null), Is.True); }
        [Test] public void StateCompare_IsCaseSensitive() { Assert.That(Needs("a", "A"), Is.True); }
    }
}
