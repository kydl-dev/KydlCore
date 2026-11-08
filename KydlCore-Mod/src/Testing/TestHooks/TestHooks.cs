using System;
using System.IO;
using System.Linq;
using KydlCore.Common;
using KydlCore.Features.Diagnostics.Contracts;
using KydlCore.Features.Hub.Contracts;
using KydlCore.Features.Items.Contracts;

namespace KydlCore.Testing.TestHooks;

/// <summary>
/// KydlCore D-084: hooks for DrakesTestLab (KydlCore-Mod/tests/TestLab/suites/KydlCore). They call the REAL hub / registry code; reads return short values a suite can compare.
/// </summary>
internal static class TestHooks
{
    /// Every test starts here (MIGRATION_DECISIONS C26, lessons TESTLAB_PLAYBOOK): the player on the open arena spot 70 m from the lab start, facing +z, god mode on.
    [TestHook("kc.arena")]
    static string Arena() { return TestArena.MovePlayer(); }

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

    /// <summary>Spec 5: the real KyItems.Contribute with a game prefab (listing only, never added to the game). Returns the KyResult text.</summary>
    [TestHook("kc.itemsContribute")]
    static string ItemsContribute(string id, string prefab, string owner)
    {
        UnityEngine.GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
        if (go == null && ObjectDB.instance != null) go = ObjectDB.instance.GetItemPrefab(prefab);
        return KyItems.Contribute(new KyContribution { Id = id, ModGuid = owner, Prefab = go, Kind = KyKind.Item }).ToString();
    }

    /// <summary>The prefab name listed under a contribution id ("" when none).</summary>
    [TestHook("kc.itemsById")]
    static string ItemsById(string id) { var e = KyItems.FindById(id); return e != null ? e.Prefab : ""; }

    [TestHook("kc.itemsWithdraw")]
    static bool ItemsWithdraw(string id) { return KyItems.Withdraw(id); }

    /// <summary>1.1.0: the real FlightRecorder.Track on the local player (with one fact text). Returns whether it is tracked.</summary>
    [TestHook("kc.flightTrackPlayer")]
    static bool FlightTrackPlayer(string label) { return FlightRecorder.Track(Player.m_localPlayer, label, () => "lab fact"); }

    [TestHook("kc.flightUntrack")]
    static bool FlightUntrack() { return FlightRecorder.Untrack(Player.m_localPlayer); }

    [TestHook("kc.flightNote")]
    static bool FlightNote(string text) { return FlightRecorder.Note(text); }

    /// <summary>The real FlightRecorder.Dump: the dump file's path ("" when none was written).</summary>
    [TestHook("kc.flightDump")]
    static string FlightDump(string reason) { return FlightRecorder.Dump(reason) ?? ""; }

    /// <summary>Whether the newest dump file of this game start exists and holds the text.</summary>
    [TestHook("kc.flightDumpHas")]
    static bool FlightDumpHas(string text)
    {
        string path = FlightRecorder.LastDumpPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        try { return File.ReadAllText(path).Contains(text); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
