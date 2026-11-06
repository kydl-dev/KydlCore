using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace KydlCore.Tests.Unit;

/// <summary>
/// ModLog.State / Decision / Every / IsDue (user, 2026-11-06: diagnostics from the start): debug lines in the log, their return values, and
/// the throttle of Every. Reached by reflection in the built DLL, like ModLogTests.
/// </summary>
[TestFixture]
public class ModLogDiagnosticsTests
{
    const string LogType = "KydlCore.Common.ModLog";
    const string ContextsType = "KydlCore.Common.ModContexts";

    sealed class CaptureListener : ILogListener
    {
        public readonly List<string> Lines = new List<string>();
        public void LogEvent(object sender, LogEventArgs eventArgs) { Lines.Add(Convert.ToString(eventArgs.Data)); }
        public void Dispose() { }
    }

    CaptureListener _listener;
    ManualLogSource _source;
    bool _debug;

    [SetUp]
    public void SetUp()
    {
        ModAssembly.Call(ContextsType, "ResetAllForTests");
        _debug = true;
        _listener = new CaptureListener();
        _source = new ManualLogSource("KydlCoreDiagTest");
        BepInEx.Logging.Logger.Listeners.Add(_listener);
        BepInEx.Logging.Logger.Sources.Add(_source);
        ModAssembly.Call(LogType, "Init", _source, "KydlCore", (Func<bool>)(() => _debug));
    }

    [TearDown]
    public void TearDown()
    {
        BepInEx.Logging.Logger.Sources.Remove(_source);
        BepInEx.Logging.Logger.Listeners.Remove(_listener);
        _source.Dispose();
        _listener.Dispose();
        ModAssembly.Call(ContextsType, "ResetAllForTests");
    }

    List<string> Written() { return _listener.Lines.Skip(1).ToList(); }       // after the session marker

    [Test]
    public void State_WritesFromToAndReason_ReturnsWhetherItChanged()
    {
        var changed = (bool)ModAssembly.Call(LogType, "State", "guard", "idle", "fighting", "Greyling attacks the owner");
        var same = (bool)ModAssembly.Call(LogType, "State", "guard", "idle", "idle", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);
            Assert.That(same, Is.False);
            Assert.That(Written()[0], Does.EndWith("STATE guard: idle -> fighting (Greyling attacks the owner)"));
            Assert.That(Written()[1], Does.EndWith("STATE guard: idle -> idle"));
        }
    }

    [Test]
    public void State_DebugOff_WritesNothing()
    {
        _debug = false;

        ModAssembly.Call(LogType, "State", "guard", "idle", "fighting", "why");

        Assert.That(Written(), Is.Empty);
    }

    [Test]
    public void Decision_WritesInputs_ReturnsTheOutcome()
    {
        var outcome = (string)ModAssembly.Call(LogType, "Decision", "next target", "Beech1", "ordered source, 12 m");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo("Beech1"));
            Assert.That(Written()[0], Does.EndWith("DECISION next target -> Beech1 (ordered source, 12 m)"));
        }
    }

    [Test]
    public void Decision_NullOutcome_IsWrittenAsNone()
    {
        ModAssembly.Call(LogType, "Decision", "next target", null, null);

        Assert.That(Written()[0], Does.EndWith("DECISION next target -> (none)"));
    }

    [Test]
    public void Every_FirstCallIsDue_SecondAtOnceIsNot()
    {
        var first = (bool)ModAssembly.Call(LogType, "Every", "chase", 60.0, "speed 3 m/s");
        var second = (bool)ModAssembly.Call(LogType, "Every", "chase", 60.0, "speed 4 m/s");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(Written(), Has.Count.EqualTo(1));
            Assert.That(Written()[0], Does.EndWith("STATUS chase: speed 3 m/s"));
        }
    }

    [Test]
    public void Every_OtherKeys_HaveTheirOwnSlot()
    {
        ModAssembly.Call(LogType, "Every", "chase", 60.0, "a");

        Assert.That((bool)ModAssembly.Call(LogType, "Every", "farm", 60.0, "b"), Is.True);
    }

    [Test]
    public void Every_ZeroSeconds_IsAlwaysDue()
    {
        ModAssembly.Call(LogType, "Every", "tick", 0.0, "a");

        Assert.That((bool)ModAssembly.Call(LogType, "Every", "tick", 0.0, "b"), Is.True);
    }

    [Test]
    public void Every_DebugOff_StillTakesTheSlot_ButWritesNothing()
    {
        _debug = false;

        var due = (bool)ModAssembly.Call(LogType, "Every", "chase", 60.0, "a");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(due, Is.True, "the flight recorder gets the line even with the debug log off");
            Assert.That(Written(), Is.Empty);
            Assert.That((bool)ModAssembly.Call(LogType, "IsDue", "chase", 60.0), Is.False);
        }
    }

    [Test]
    public void IsDue_DoesNotUseUpTheSlot()
    {
        ModAssembly.Call(LogType, "IsDue", "chase", 60.0);

        Assert.That((bool)ModAssembly.Call(LogType, "Every", "chase", 60.0, "a"), Is.True);
    }
}
