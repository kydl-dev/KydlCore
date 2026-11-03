using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace KydlCore.Tests.Unit
{
    /// <summary>
    /// KyUi.Open with a window BUILDER (D-086): Core runs the builder as the mod (KydlCore.Ui.UiLog.Owner = the mod's GUID), so the kit's log lines of the
    /// window carry the mod's tag. Reached in the BUILT mod DLL by reflection; the test project has no UIElements reference, so the builder delegate is
    /// made with an expression tree. Only the paths that end before the UI root is touched run here (no engine in a unit test).
    /// </summary>
    [TestFixture]
    public class KyUiTests
    {
        static string seenOwner;
        static int builds;

        public static void Record() { builds++; seenOwner = (string)ModAssembly.GetStatic("KydlCore.Ui.UiLog", "Owner"); }
        public static void Throw() { builds++; throw new InvalidOperationException("builder broke"); }

        [SetUp]
        public void Reset() { seenOwner = null; builds = 0; ModAssembly.SetStatic("KydlCore.Ui.UiLog", "Owner", null); }

        static MethodInfo BuilderOverload()
        {
            return ModAssembly.Type("KydlCore.Features.Hub.Contracts.KyUi").GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "Open" && m.GetParameters()[2].ParameterType.IsGenericType);
        }

        /// A Func&lt;VisualElement&gt; that calls `step` and returns null.
        static Delegate Builder(string step)
        {
            var funcType = BuilderOverload().GetParameters()[2].ParameterType;
            var element = funcType.GetGenericArguments()[0];
            var body = Expression.Block(Expression.Call(typeof(KyUiTests).GetMethod(step)), Expression.Constant(null, element));
            return Expression.Lambda(funcType, body).Compile();
        }

        static object Open(string guid, string id, Delegate build)
        {
            try { return BuilderOverload().Invoke(null, new object[] { guid, id, build, null }); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        [Test]
        public void Open_WithBuilder_IsPublic_AndTakesAFuncOfVisualElement()
        {
            var p = BuilderOverload().GetParameters();
            Assert.That(p[2].ParameterType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Func<>)));
            Assert.That(p[2].ParameterType.GetGenericArguments()[0].Name, Is.EqualTo("VisualElement"));
        }

        [Test]
        public void Open_NullBuilder_GivesNull() { Assert.That(Open("com.kydldev.A", "w", null), Is.Null); }

        [Test]
        public void Open_RunsTheBuilderAsTheMod_AndPutsTheOwnerBack()
        {
            var result = Open("com.kydldev.A", "w", Builder("Record"));
            Assert.That(builds, Is.EqualTo(1));
            Assert.That(seenOwner, Is.EqualTo("com.kydldev.A"));
            Assert.That(ModAssembly.GetStatic("KydlCore.Ui.UiLog", "Owner"), Is.Null);
            Assert.That(result, Is.Null, "a builder that returns no window opens nothing");
        }

        [Test]
        public void Open_ThrowingBuilder_GivesNull_AndPutsTheOwnerBack()
        {
            Assert.That(Open("com.kydldev.A", "w", Builder("Throw")), Is.Null);
            Assert.That(builds, Is.EqualTo(1));
            Assert.That(ModAssembly.GetStatic("KydlCore.Ui.UiLog", "Owner"), Is.Null);
        }
    }
}
