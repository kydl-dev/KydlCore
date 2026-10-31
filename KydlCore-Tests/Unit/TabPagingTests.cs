using System;
using System.Linq;
using System.Reflection;
using FsCheck;

namespace KydlCore.Tests.Unit
{
    /// <summary>
    /// TabPaging (PLAN T-035, D-027): lines of a wrapped tab row, at most 2 shown, with arrows / scroll bar for more. The class is reached in the
    /// BUILT mod DLL by reflection (like WindowStack) so Stryker can mutate it.
    /// </summary>
    [TestFixture]
    public class TabPagingTests
    {
        static readonly Type P = ModAssembly.Type("KydlCore.Ui.TabPaging");

        static object Call(string name, params object[] args) { return P.GetMethod(name).Invoke(null, args); }
        static float[] Tops(float[] childTops, float tol = 8f) { return (float[])Call("RowTops", childTops, tol); }
        static int RowOf(float[] childTops, int index, float tol = 8f) { return (int)Call("RowOf", childTops, tol, index); }
        static bool Needed(int rows) { return (bool)Call("Needed", rows); }
        static int MaxTop(int rows) { return (int)Call("MaxTop", rows); }
        static int Clamp(int top, int rows) { return (int)Call("Clamp", top, rows); }
        static int Step(int top, int delta, int rows) { return (int)Call("Step", top, delta, rows); }
        static int Reveal(int row, int top, int rows) { return (int)Call("Reveal", row, top, rows); }
        static float Offset(float[] rowTops, int top) { return (float)Call("Offset", rowTops, top); }
        static float ViewHeight(float[] rowTops, float content, int top) { return (float)Call("ViewHeight", rowTops, content, top); }
        static float ThumbStart(int rows, int top) { return (float)Call("ThumbStart", rows, top); }
        static float ThumbLength(int rows) { return (float)Call("ThumbLength", rows); }

        [Test]
        public void MaxRows_IsTwo()
        {
            Assert.That(P.GetField("MaxRows").GetRawConstantValue(), Is.EqualTo(2));
        }

        // --- RowTops / RowOf --------------------------------------------------------------------------------------------------
        [Test]
        public void RowTops_OneLine_OneRow()
        {
            Assert.That(Tops(new[] { 10f, 10f, 10f }), Is.EqualTo(new[] { 10f }));
        }

        [Test]
        public void RowTops_TabsOnOneLineMayDifferByTheLift()
        {
            Assert.That(Tops(new[] { 10f, 13f, 13f, 10f }), Is.EqualTo(new[] { 10f }));
        }

        [Test]
        public void RowTops_TheLineTopIsItsHighestTab()
        {
            Assert.That(Tops(new[] { 13f, 10f, 13f }), Is.EqualTo(new[] { 10f }));
        }

        [Test]
        public void RowTops_ANewLineStartsBelowTheTolerance()
        {
            Assert.That(Tops(new[] { 10f, 10f, 50f, 53f, 90f }), Is.EqualTo(new[] { 10f, 50f, 90f }));
        }

        [Test]
        public void RowTops_ExactlyTheToleranceStaysOnTheLine()
        {
            Assert.That(Tops(new[] { 10f, 18f }, 8f), Is.EqualTo(new[] { 10f }));
            Assert.That(Tops(new[] { 10f, 18.5f }, 8f), Is.EqualTo(new[] { 10f, 18.5f }));
        }

        [Test]
        public void RowTops_NoTabs_NoRows()
        {
            Assert.That(Tops(new float[0]), Is.Empty);
            Assert.That(Call("RowTops", null, 8f), Is.Empty);
        }

        [Test]
        public void RowOf_CountsLines()
        {
            var tops = new[] { 10f, 13f, 50f, 53f, 90f };
            Assert.That(RowOf(tops, 0), Is.EqualTo(0));
            Assert.That(RowOf(tops, 1), Is.EqualTo(0));
            Assert.That(RowOf(tops, 2), Is.EqualTo(1));
            Assert.That(RowOf(tops, 3), Is.EqualTo(1));
            Assert.That(RowOf(tops, 4), Is.EqualTo(2));
        }

        [Test]
        public void RowOf_BadIndex_IsMinusOne()
        {
            var tops = new[] { 10f, 50f };
            Assert.That(RowOf(tops, -1), Is.EqualTo(-1));
            Assert.That(RowOf(tops, 2), Is.EqualTo(-1));
            Assert.That(Call("RowOf", null, 8f, 0), Is.EqualTo(-1));
        }

        [Test]
        public void RowOf_LastIndexIsInTheLastLine()
        {
            Assert.That(RowOf(new[] { 10f, 50f, 90f }, 2), Is.EqualTo(2));
        }

        // --- Needed / MaxTop / Clamp / Step ------------------------------------------------------------------------------------
        [Test]
        public void Needed_OnlyAboveTwoRows()
        {
            Assert.That(Needed(0), Is.False);
            Assert.That(Needed(1), Is.False);
            Assert.That(Needed(2), Is.False);
            Assert.That(Needed(3), Is.True);
            Assert.That(Needed(10), Is.True);
        }

        [Test]
        public void MaxTop_IsTheLastPage()
        {
            Assert.That(MaxTop(0), Is.EqualTo(0));
            Assert.That(MaxTop(2), Is.EqualTo(0));
            Assert.That(MaxTop(3), Is.EqualTo(1));
            Assert.That(MaxTop(7), Is.EqualTo(5));
        }

        [Test]
        public void Clamp_KeepsTopInside()
        {
            Assert.That(Clamp(-3, 5), Is.EqualTo(0));
            Assert.That(Clamp(2, 5), Is.EqualTo(2));
            Assert.That(Clamp(3, 5), Is.EqualTo(3));
            Assert.That(Clamp(4, 5), Is.EqualTo(3));
            Assert.That(Clamp(9, 2), Is.EqualTo(0));
        }

        [Test]
        public void Step_MovesOneLineAndStopsAtTheEnds()
        {
            Assert.That(Step(0, 1, 5), Is.EqualTo(1));
            Assert.That(Step(3, 1, 5), Is.EqualTo(3), "already on the last page");
            Assert.That(Step(1, -1, 5), Is.EqualTo(0));
            Assert.That(Step(0, -1, 5), Is.EqualTo(0));
            Assert.That(Step(2, 0, 5), Is.EqualTo(2));
            Assert.That(Step(0, 1, 2), Is.EqualTo(0), "nothing to scroll");
        }

        // --- Reveal -------------------------------------------------------------------------------------------------------------
        [Test]
        public void Reveal_RowAlreadyShown_KeepsTop()
        {
            Assert.That(Reveal(2, 2, 6), Is.EqualTo(2));
            Assert.That(Reveal(3, 2, 6), Is.EqualTo(2));
        }

        [Test]
        public void Reveal_RowAbove_MovesTopUpToIt()
        {
            Assert.That(Reveal(1, 3, 6), Is.EqualTo(1));
        }

        [Test]
        public void Reveal_RowBelow_MovesTopSoItIsTheSecondLine()
        {
            Assert.That(Reveal(4, 0, 6), Is.EqualTo(3));
            Assert.That(Reveal(2, 0, 6), Is.EqualTo(1));
        }

        [Test]
        public void Reveal_NeverLeavesThePages()
        {
            Assert.That(Reveal(5, 0, 6), Is.EqualTo(4));
            Assert.That(Reveal(-2, 1, 6), Is.EqualTo(0));
            Assert.That(Reveal(0, 0, 1), Is.EqualTo(0));
        }

        [Test]
        public void Property_Reveal_ShowsTheRow_WheneverTheRowExists()
        {
            var g = from rows in Gen.Choose(1, 12) from row in Gen.Choose(0, rows - 1) from top in Gen.Choose(0, 12) select new[] { rows, row, top };
            Props.Check(Prop.ForAll(g.ToArbitrary(), a =>
            {
                int t = Reveal(a[1], Clamp(a[2], a[0]), a[0]);
                return t >= 0 && t <= MaxTop(a[0]) && a[1] >= t && a[1] < t + 2;
            }));
        }

        [Test]
        public void Property_Step_AlwaysStaysInsideThePages()
        {
            var g = from rows in Gen.Choose(0, 12) from top in Gen.Choose(-3, 15) from d in Gen.Choose(-3, 3) select new[] { rows, top, d };
            Props.Check(Prop.ForAll(g.ToArbitrary(), a =>
            {
                int t = Step(a[1], a[2], a[0]);
                return t >= 0 && t <= MaxTop(a[0]);
            }));
        }

        // --- Offset / ViewHeight ------------------------------------------------------------------------------------------------
        [Test]
        public void Offset_IsTheDistanceFromTheFirstLine()
        {
            var tops = new[] { 10f, 50f, 90f, 130f };
            Assert.That(Offset(tops, 0), Is.EqualTo(0f));
            Assert.That(Offset(tops, 1), Is.EqualTo(40f));
            Assert.That(Offset(tops, 2), Is.EqualTo(80f));
        }

        [Test]
        public void Offset_BadTopOrNoRows_IsSafe()
        {
            var tops = new[] { 10f, 50f };
            Assert.That(Offset(tops, 9), Is.EqualTo(40f), "past the end: the last line");
            Assert.That(Offset(tops, -4), Is.EqualTo(0f));
            Assert.That(Offset(new float[0], 1), Is.EqualTo(0f));
            Assert.That(Call("Offset", null, 1), Is.EqualTo(0f));
        }

        [Test]
        public void ViewHeight_TwoLinesFromTop()
        {
            var tops = new[] { 10f, 50f, 90f, 130f };
            Assert.That(ViewHeight(tops, 170f, 0), Is.EqualTo(80f));
            Assert.That(ViewHeight(tops, 170f, 1), Is.EqualTo(80f));
        }

        [Test]
        public void ViewHeight_LastPage_EndsAtTheContentEnd()
        {
            var tops = new[] { 10f, 50f, 90f, 130f };
            Assert.That(ViewHeight(tops, 170f, 2), Is.EqualTo(80f));
            Assert.That(ViewHeight(new[] { 10f, 50f, 90f }, 130f, 1), Is.EqualTo(80f));
        }

        [Test]
        public void ViewHeight_FewerThanTwoLines_IsAllOfIt()
        {
            Assert.That(ViewHeight(new[] { 10f }, 50f, 0), Is.EqualTo(40f));
            Assert.That(ViewHeight(new[] { 10f, 50f }, 90f, 0), Is.EqualTo(80f));
        }

        [Test]
        public void ViewHeight_NoRows_IsTheContentHeight()
        {
            Assert.That(ViewHeight(new float[0], 33f, 0), Is.EqualTo(33f));
            Assert.That(Call("ViewHeight", null, 33f, 0), Is.EqualTo(33f));
        }

        [Test]
        public void ViewHeight_NeverNegative()
        {
            Assert.That(ViewHeight(new[] { 10f, 50f, 90f }, 20f, 2), Is.EqualTo(0f));
        }

        [Test]
        public void ViewHeight_BadTop_IsSafe()
        {
            Assert.That(ViewHeight(new[] { 10f, 50f, 90f }, 130f, 9), Is.EqualTo(40f));
            Assert.That(ViewHeight(new[] { 10f, 50f, 90f }, 130f, -9), Is.EqualTo(80f));
        }

        // --- scroll bar thumb ---------------------------------------------------------------------------------------------------
        [Test]
        public void ThumbLength_IsTheShownShare()
        {
            Assert.That(ThumbLength(4), Is.EqualTo(0.5f));
            Assert.That(ThumbLength(3), Is.EqualTo(2f / 3f).Within(1e-6));
            Assert.That(ThumbLength(2), Is.EqualTo(1f));
            Assert.That(ThumbLength(0), Is.EqualTo(1f));
        }

        [Test]
        public void ThumbStart_IsTheTopShare()
        {
            Assert.That(ThumbStart(4, 0), Is.EqualTo(0f));
            Assert.That(ThumbStart(4, 2), Is.EqualTo(0.5f));
            Assert.That(ThumbStart(0, 0), Is.EqualTo(0f));
            Assert.That(ThumbStart(4, 9), Is.EqualTo(0.5f), "top is clamped first");
        }

        [Test]
        public void Property_Thumb_StaysInsideTheTrack()
        {
            var g = from rows in Gen.Choose(0, 20) from top in Gen.Choose(-2, 25) select new[] { rows, top };
            Props.Check(Prop.ForAll(g.ToArbitrary(), a =>
            {
                float s = ThumbStart(a[0], a[1]), l = ThumbLength(a[0]);
                return s >= 0f && l > 0f && s + l <= 1.0001f;
            }));
        }
    }
}
