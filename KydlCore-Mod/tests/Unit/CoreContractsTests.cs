// Unit tests of the shared pure contracts (KydlCore.Core, src/Shared/Core). Moved here from Claude\shared\Kydl.Core.Tests on 2026-11-03.
using System;
using System.Collections.Generic;
using KydlCore.Core;

namespace KydlCore.Tests.Unit
{
    public class ResultsTests
    {
        [Test] public void Factories_CarryStatusValueAndReason()
        {
            var s = OperationResponse<int>.Success(4, "ok");
            Assert.That(s.IsSuccess, Is.True); Assert.That(s.Value, Is.EqualTo(4)); Assert.That(s.ToString(), Is.EqualTo("Success: ok"));
            Assert.That(OperationResponse<int>.Rejected("no").Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(OperationResponse<int>.Ignored("idle").Status, Is.EqualTo(ResponseStatus.Ignored));
            Assert.That(OperationResponse<int>.Failed("boom").Value, Is.EqualTo(0));
            Assert.That(new OperationResponse<int>(ResponseStatus.Success, 1, null).Reason, Is.EqualTo(""));
        }

        [Test] public void As_KeepsStatusAndReason()
        {
            var r = OperationResponse<int>.Rejected("full").As<string>();
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo("full")); Assert.That(r.Value, Is.Null);
        }

        [Test] public void Validation_Helpers()
        {
            Assert.That(ValidationResult.RejectUnless(true, "x").IsValid, Is.True);
            Assert.That(ValidationResult.RejectUnless(false, "x").Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(ValidationResult.IgnoreUnless(false, "y").Status, Is.EqualTo(ResponseStatus.Ignored));
            Assert.That(ValidationResult.Valid.ToString(), Is.EqualTo("Valid"));
        }
    }

    public class PipelineTests
    {
        [Test] public void FirstFailingCheck_StopsTheRun_InOrder()
        {
            var ran = new List<string>();
            var p = new OperationPipeline<int, string>("p", i => { ran.Add("decide"); return OperationResponse<string>.Success("n" + i); })
                .Validate("positive", i => { ran.Add("a"); return ValidationResult.RejectUnless(i > 0, "not positive"); })
                .Validate("small", i => { ran.Add("b"); return ValidationResult.IgnoreUnless(i < 10, "too big"); });
            var lines = new List<string>(); p.Trace = lines.Add;
            var r = p.Run(-1);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo("not positive"));
            Assert.That(ran, Is.EqualTo(new[] { "a" }));
            Assert.That(lines[0], Does.Contain("stopped at positive"));
            ran.Clear(); Assert.That(p.Run(20).Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(ran, Is.EqualTo(new[] { "a", "b" }));
            ran.Clear(); Assert.That(p.Run(3).Value, Is.EqualTo("n3")); Assert.That(ran, Is.EqualTo(new[] { "a", "b", "decide" }));
            Assert.That(p.StepCount, Is.EqualTo(2));
        }

        [Test] public void NullDecide_Throws() { Assert.Throws<ArgumentNullException>(() => new OperationPipeline<int, int>("x", (Func<int, OperationResponse<int>>)null)); }
    }

    public class CommandTests
    {
        sealed class Add : ICommand<int> { public int A, B; }
        sealed class Other : ICommand<int> { }
        sealed class AddHandler : ICommandHandler<Add, int> { public OperationResponse<int> Handle(Add c) { return OperationResponse<int>.Success(c.A + c.B); } }
        sealed class Throws : ICommandHandler<Other, int> { public OperationResponse<int> Handle(Other c) { throw new InvalidOperationException("bad"); } }

        [Test] public void Dispatch_RoutesToTheHandler()
        {
            var d = new CommandDispatcher();
            Assert.That(d.Register(new AddHandler()), Is.True);
            Assert.That(d.Dispatch(new Add { A = 2, B = 2 }).Value, Is.EqualTo(4));
        }

        [Test] public void Missing_Null_Throwing_AreFailed()
        {
            var d = new CommandDispatcher();
            Assert.That(d.Dispatch<int>(null).Status, Is.EqualTo(ResponseStatus.Failed));
            Assert.That(d.Dispatch(new Other()).Reason, Does.Contain("no handler"));
            d.Register(new Throws());
            var r = d.Dispatch(new Other());
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Failed)); Assert.That(r.Reason, Does.Contain("InvalidOperationException"));
        }
    }

    public class FeatureRegistryTests
    {
        sealed class F : IFeature
        {
            public string Id { get; set; }
            public bool On; public bool StartOk = true; public bool ThrowOnStart; public bool ThrowGate;
            public int Starts, Stops;
            public bool IsEnabled() { if (ThrowGate) throw new InvalidOperationException("gate"); return On; }
            public bool Start() { if (ThrowOnStart) throw new InvalidOperationException("start"); Starts++; return StartOk; }
            public bool Stop() { Stops++; return Stops > 0; }
        }

        [Test] public void Add_RejectsNullEmptyAndDuplicates()
        {
            var r = new FeatureRegistry();
            Assert.That(r.Add(null).Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(r.Add(new F { Id = "" }).Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(r.Add(new F { Id = "cam" }).Value, Is.EqualTo(1));
            Assert.That(r.Add(new F { Id = "CAM" }).Reason, Does.Contain("duplicate"));
        }

        [Test] public void Refresh_StartsAndStopsLive()
        {
            var cam = new F { Id = "cam", On = true }; var col = new F { Id = "col", On = false };
            var r = new FeatureRegistry(); r.Add(cam); r.Add(col);
            Assert.That(r.Refresh().Value, Is.EqualTo(1)); Assert.That(r.IsRunning("cam"), Is.True); Assert.That(r.IsRunning("col"), Is.False);
            Assert.That(r.Refresh().Value, Is.EqualTo(0));
            cam.On = false; col.On = true;
            Assert.That(r.Refresh().Value, Is.EqualTo(2));
            Assert.That(cam.Stops, Is.EqualTo(1)); Assert.That(col.Starts, Is.EqualTo(1));
            Assert.That(r.StopAll().Value, Is.EqualTo(1)); Assert.That(r.IsRunning("col"), Is.False);
        }

        [Test] public void ABrokenFeature_DoesNotStopTheOthers()
        {
            var bad = new F { Id = "bad", On = true, ThrowOnStart = true }; var half = new F { Id = "half", On = true, StartOk = false };
            var gate = new F { Id = "gate", On = true, ThrowGate = true }; var good = new F { Id = "good", On = true };
            var lines = new List<string>();
            var r = new FeatureRegistry(lines.Add); r.Add(bad); r.Add(half); r.Add(gate); r.Add(good);
            var res = r.Refresh();
            Assert.That(res.Status, Is.EqualTo(ResponseStatus.Failed)); Assert.That(res.Reason, Does.Contain("bad").And.Contain("half"));
            Assert.That(r.IsRunning("good"), Is.True); Assert.That(r.IsRunning("bad"), Is.False); Assert.That(r.IsRunning("half"), Is.False); Assert.That(r.IsRunning("gate"), Is.False);
            Assert.That(lines.Exists(l => l.Contains("treated as off")), Is.True);
        }
    }

    public class ClockTests
    {
        [Test] public void ManualClock_Advances_IgnoresNegative()
        {
            var c = new ManualClock(5f);
            Assert.That(c.Advance(2f), Is.True); Assert.That(c.Now, Is.EqualTo(7f));
            c.Advance(-3f); Assert.That(c.Now, Is.EqualTo(7f));
        }
    }
}
