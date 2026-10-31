using System.Linq;
using Kydl.Common;

namespace KydlCore.Testing
{
    /// <summary>
    /// KydlCore D-084: hooks for DrakesTestLab (KydlCore-Tests/TestLab/suites/KydlCore). They call the REAL hub / registry code; reads return short values a suite can compare.
    /// </summary>
    internal static class TestHooks
    {
        /// <summary>All Startup_* / World_* checks of InGameTests (the ones Kydl.GameProbe runs): "PASS: ..." or "FAIL: ...".</summary>
        [TestHook("kc.selftest")]
        static string SelfTest() { return TestLabSupport.RunInGameTests(typeof(InGameTests)); }

        [TestHook("kc.hubOpen")]
        static bool HubOpen(string mod = null, string tab = null)
        {
            KyHub.Open(string.IsNullOrEmpty(mod) ? null : mod, string.IsNullOrEmpty(tab) ? null : tab);
            return KyHub.IsOpen;
        }

        [TestHook("kc.hubIsOpen")]
        static bool HubIsOpen() { return KyHub.IsOpen; }

        [TestHook("kc.hubClose")]
        static bool HubClose() { KyHub.Close(); return KyHub.IsOpen; }

        /// <summary>Mods in the hub's row 1 (comma separated guids); the hub reads them when it opens.</summary>
        [TestHook("kc.hubMods")]
        static string HubMods() { return string.Join(",", KyHub.Model.ModGuids().ToArray()); }

        /// <summary>Number of row-2 tabs the mod has in the hub.</summary>
        [TestHook("kc.hubTabCount")]
        static int HubTabCount(string mod) { return KyHub.Model.TabIds(mod).Length; }

        [TestHook("kc.hubTabs")]
        static string HubTabs(string mod) { return string.Join(",", KyHub.Model.TabIds(mod)); }

        /// <summary>Number of KY-Items entries of one mod (0 = all mods).</summary>
        [TestHook("kc.itemsCount")]
        static int ItemsCount(string mod = null)
        {
            return string.IsNullOrEmpty(mod) ? KyItems.All.Count : KyItems.ByMod(mod).Count();
        }

        [TestHook("kc.itemsReady")]
        static bool ItemsReady() { return KyItems.Ready; }
    }
}
