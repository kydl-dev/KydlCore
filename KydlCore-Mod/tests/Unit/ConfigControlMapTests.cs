using System;
using System.Collections.Generic;
using System.Reflection;
using FsCheck;
using NUnit.Framework;

namespace KydlCore.Tests.Unit
{
    /// <summary>
    /// ConfigControlMap (PLAN T-033, D-023): which control a config entry gets, how typed number text is read and shown, how a slider value is
    /// clamped, and how the "Browsable = false" / "IsAdminOnly = true" tags are found (by member name, in any tag class). The class under test is
    /// reached in the BUILT mod DLL by reflection (like WindowStack) so Stryker can mutate it.
    /// </summary>
    [TestFixture]
    public class ConfigControlMapTests
    {
        static readonly Type Map = ModAssembly.Type("KydlCore.Presentation.Hub.Pages.ConfigControlMap");
        static readonly Type KindType = ModAssembly.Type("KydlCore.Presentation.Hub.Pages.ControlKind");

        static object Call(string name, params object[] args) { return Map.GetMethod(name).Invoke(null, args); }
        static string Kind(Type t, bool range = false, bool list = false) { return Call("Classify", t, range, list).ToString(); }
        static object Parse(Type t, string s) { return Call("Parse", t, s); }
        static string Format(Type t, object v) { return (string)Call("Format", t, v); }
        static object Clamp(Type t, double v, double min, double max) { return Call("Clamp", t, v, min, max); }
        static bool Hidden(params object[] tags) { return (bool)Call("IsHidden", new List<object>(tags)); }
        static bool Admin(params object[] tags) { return (bool)Call("IsAdminOnly", new List<object>(tags)); }
        static bool ReadOnly(params object[] tags) { return (bool)Call("IsReadOnly", new List<object>(tags)); }
        sealed class ReadOnlyFieldTag { public bool? ReadOnly = false; }       // like ServerSync's ConfigurationManagerAttributes
        sealed class ReadOnlyPropertyTag { public bool ReadOnly { get; set; } }

        [Test] public void ReadOnly_NoTags_IsFalse() { Assert.That(ReadOnly(), Is.False); Assert.That(Call("IsReadOnly", new object[] { null }), Is.False); }
        [Test] public void ReadOnly_TrueField_IsTrue() { Assert.That(ReadOnly(new ReadOnlyFieldTag { ReadOnly = true }), Is.True); }
        [Test] public void ReadOnly_FalseField_IsFalse() { Assert.That(ReadOnly(new ReadOnlyFieldTag { ReadOnly = false }), Is.False); }
        [Test] public void ReadOnly_NullField_IsFalse() { Assert.That(ReadOnly(new ReadOnlyFieldTag { ReadOnly = null }), Is.False); }
        [Test] public void ReadOnly_TrueProperty_IsTrue() { Assert.That(ReadOnly(new ReadOnlyPropertyTag { ReadOnly = true }), Is.True); }
        [Test] public void ReadOnly_UnrelatedAndNullTags_AreIgnored() { Assert.That(ReadOnly(null, new UnrelatedTag(), new ReadOnlyFieldTag { ReadOnly = true }), Is.True); }
        [Test] public void ReadOnly_IsNotHiddenNorAdminOnly() { var t = new ReadOnlyFieldTag { ReadOnly = true }; Assert.That(Hidden(t), Is.False); Assert.That(Admin(t), Is.False); }

        // fake ConfigurationManagerAttributes classes: a public field, a public property, a private field, a bool? field, a wrong-typed member
        sealed class FieldTag { public bool Browsable = true; public bool IsAdminOnly = false; }
        sealed class PropertyTag { public bool Browsable { get; set; } public bool IsAdminOnly { get; set; } }
        sealed class NullableTag { public bool? Browsable; public bool? IsAdminOnly; }
        sealed class PrivateTag { bool Browsable = false; bool IsAdminOnly = true; public override string ToString() { return "" + Browsable + IsAdminOnly; } }
        sealed class WrongTypeTag { public string Browsable = "false"; public int IsAdminOnly = 1; }
        sealed class IndexerTag { public bool this[int i] { get { return false; } } }
        sealed class UnrelatedTag { public int Order = 3; }

        // --- Classify ---------------------------------------------------------------------------------------------------------
        [Test] public void Classify_Bool_IsToggle() { Assert.That(Kind(typeof(bool)), Is.EqualTo("Toggle")); }
        [Test] public void Classify_Bool_IgnoresRangeAndList() { Assert.That(Kind(typeof(bool), true, true), Is.EqualTo("Toggle")); }
        [Test] public void Classify_KeyCode_IsKey_NotDropdown() { Assert.That(Kind(typeof(UnityEngine.KeyCode)), Is.EqualTo("Key")); }
        [Test] public void Classify_KeyCode_WithListOrRange_IsStillKey() { Assert.That(Kind(typeof(UnityEngine.KeyCode), true, true), Is.EqualTo("Key")); }
        [Test] public void Classify_KeyboardShortcut_IsShortcut()
        {
            var shortcut = Type.GetType("BepInEx.Configuration.KeyboardShortcut, BepInEx", true);
            Assert.That(Kind(shortcut), Is.EqualTo("Shortcut"));
        }
        [Test] public void Classify_Enum_IsDropdown() { Assert.That(Kind(typeof(DayOfWeek)), Is.EqualTo("Dropdown")); }
        [Test] public void Classify_Enum_WithRange_IsStillDropdown() { Assert.That(Kind(typeof(DayOfWeek), true), Is.EqualTo("Dropdown")); }
        [Test] public void Classify_String_IsText() { Assert.That(Kind(typeof(string)), Is.EqualTo("Text")); }
        [Test] public void Classify_StringWithList_IsDropdown() { Assert.That(Kind(typeof(string), false, true), Is.EqualTo("Dropdown")); }
        [Test] public void Classify_StringWithRangeOnly_IsText() { Assert.That(Kind(typeof(string), true, false), Is.EqualTo("Text")); }
        [Test] public void Classify_IntWithRange_IsSlider() { Assert.That(Kind(typeof(int), true), Is.EqualTo("Slider")); }
        [Test] public void Classify_FloatWithRange_IsSlider() { Assert.That(Kind(typeof(float), true), Is.EqualTo("Slider")); }
        [Test] public void Classify_DoubleWithRange_IsSlider() { Assert.That(Kind(typeof(double), true), Is.EqualTo("Slider")); }
        [Test] public void Classify_IntWithoutRange_IsNumber() { Assert.That(Kind(typeof(int)), Is.EqualTo("Number")); }
        [Test] public void Classify_FloatWithoutRange_IsNumber() { Assert.That(Kind(typeof(float)), Is.EqualTo("Number")); }
        [Test] public void Classify_DoubleWithoutRange_IsNumber() { Assert.That(Kind(typeof(double)), Is.EqualTo("Number")); }
        [Test] public void Classify_Long_IsNumberEvenWithRange() { Assert.That(Kind(typeof(long), true), Is.EqualTo("Number")); }
        [Test] public void Classify_NumberWithList_IsNotDropdown() { Assert.That(Kind(typeof(int), false, true), Is.EqualTo("Number")); }
        // a type whose full name is UnityEngine.Color, like the game's (built here so the test needs no Unity DLL)
        static readonly Type FakeUnityColor = System.Reflection.Emit.AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("FakeUnity"), System.Reflection.Emit.AssemblyBuilderAccess.Run)
            .DefineDynamicModule("FakeUnity").DefineType("UnityEngine.Color", TypeAttributes.Public | TypeAttributes.Sealed, typeof(ValueType)).CreateType();

        struct Color { }

        [Test] public void Classify_UnityEngineColor_IsColor() { Assert.That(Kind(FakeUnityColor), Is.EqualTo("Color")); }
        [Test] public void Classify_AnotherColorType_IsUnsupported() { Assert.That(Kind(typeof(Color)), Is.EqualTo("Unsupported")); }
        [Test] public void ControlKind_HasTheNineKinds() { Assert.That(Enum.GetNames(KindType), Is.EquivalentTo(new[] { "Unsupported", "Toggle", "Slider", "Number", "Text", "Dropdown", "Color", "Key", "Shortcut" })); }

        // --- IsNumber / Format ------------------------------------------------------------------------------------------------
        [Test]
        public void IsNumber_OnlyTheFourNumberTypes()
        {
            foreach (var t in new[] { typeof(int), typeof(long), typeof(float), typeof(double) }) Assert.That(Call("IsNumber", t), Is.True, t.Name);
            foreach (var t in new[] { typeof(short), typeof(byte), typeof(decimal), typeof(string), typeof(bool), null }) Assert.That(Call("IsNumber", t), Is.False, t == null ? "null" : t.Name);
        }

        [Test] public void Format_Int_IsPlain() { Assert.That(Format(typeof(int), 42), Is.EqualTo("42")); }
        [Test] public void Format_NegativeLong() { Assert.That(Format(typeof(long), -9000000000L), Is.EqualTo("-9000000000")); }
        [Test] public void Format_Float_TwoDecimalsAtMost() { Assert.That(Format(typeof(float), 1.256f), Is.EqualTo("1.26")); }
        [Test] public void Format_Float_WholeHasNoDecimals() { Assert.That(Format(typeof(float), 5f), Is.EqualTo("5")); }
        [Test] public void Format_Double_UsesADot() { Assert.That(Format(typeof(double), 0.5), Is.EqualTo("0.5")); }
        [Test]
        public void Format_DoesNotUseTheCurrentCulture()
        {
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.That(Format(typeof(double), 1234.5), Is.EqualTo("1234.5"));
                Assert.That(Format(typeof(int), 1234), Is.EqualTo("1234"));
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = old; }
        }
        [Test] public void Format_Null_IsEmpty() { Assert.That(Format(typeof(int), null), Is.EqualTo("")); }
        [Test] public void Format_NonNumber_IsToString() { Assert.That(Format(typeof(string), "abc"), Is.EqualTo("abc")); }
        [Test] public void Format_Bool_IsToString() { Assert.That(Format(typeof(bool), true), Is.EqualTo("True")); }
        [Test]
        public void SliderFormat_WholeAndFraction()
        {
            Assert.That(Call("SliderFormat", typeof(int)), Is.EqualTo("0"));
            Assert.That(Call("SliderFormat", typeof(long)), Is.EqualTo("0"));
            Assert.That(Call("SliderFormat", typeof(float)), Is.EqualTo("0.##"));
            Assert.That(Call("SliderFormat", typeof(double)), Is.EqualTo("0.##"));
        }

        // --- Parse ------------------------------------------------------------------------------------------------------------
        [Test] public void Parse_Int() { Assert.That(Parse(typeof(int), "42"), Is.EqualTo(42).And.TypeOf<int>()); }
        [Test] public void Parse_NegativeInt() { Assert.That(Parse(typeof(int), "-7"), Is.EqualTo(-7)); }
        [Test] public void Parse_IntWithSpaces() { Assert.That(Parse(typeof(int), "  12 "), Is.EqualTo(12)); }
        [Test] public void Parse_IntRejectsAFraction() { Assert.That(Parse(typeof(int), "1.5"), Is.Null); }
        [Test] public void Parse_IntRejectsACommaFraction() { Assert.That(Parse(typeof(int), "1,5"), Is.Null); }
        [Test] public void Parse_IntRejectsOutOfRange() { Assert.That(Parse(typeof(int), "2147483648"), Is.Null); }
        [Test]
        public void Parse_IntKeepsTheLimits()
        {
            Assert.That(Parse(typeof(int), "2147483647"), Is.EqualTo(int.MaxValue));
            Assert.That(Parse(typeof(int), "-2147483648"), Is.EqualTo(int.MinValue));
            Assert.That(Parse(typeof(int), "-2147483649"), Is.Null);
        }
        [Test] public void Parse_Long() { Assert.That(Parse(typeof(long), "9000000000"), Is.EqualTo(9000000000L).And.TypeOf<long>()); }
        [Test] public void Parse_Float() { Assert.That(Parse(typeof(float), "2.5"), Is.EqualTo(2.5f).And.TypeOf<float>()); }
        [Test] public void Parse_FloatWithComma() { Assert.That(Parse(typeof(float), "2,5"), Is.EqualTo(2.5f)); }
        [Test] public void Parse_FloatLeadingDot() { Assert.That(Parse(typeof(float), ".5"), Is.EqualTo(0.5f)); }
        [Test] public void Parse_FloatExponent() { Assert.That(Parse(typeof(float), "1e2"), Is.EqualTo(100f)); }
        [Test] public void Parse_FloatTooBig_IsNull() { Assert.That(Parse(typeof(float), "1e39"), Is.Null); }
        [Test] public void Parse_FloatTooBigNegative_IsNull() { Assert.That(Parse(typeof(float), "-1e39"), Is.Null); }
        [Test] public void Parse_FloatMax_IsKept() { Assert.That(Parse(typeof(float), "3.4028234663852886E+38"), Is.EqualTo(float.MaxValue)); }
        [Test] public void Parse_FloatMinusMax_IsKept() { Assert.That(Parse(typeof(float), "-3.4028234663852886E+38"), Is.EqualTo(-float.MaxValue)); }
        [Test] public void Format_LongKeepsEveryDigit() { Assert.That(Format(typeof(long), 9007199254740993L), Is.EqualTo("9007199254740993")); }
        [Test] public void Parse_LongKeepsEveryDigit() { Assert.That(Parse(typeof(long), "9007199254740993"), Is.EqualTo(9007199254740993L)); }
        [Test] public void Parse_Double() { Assert.That(Parse(typeof(double), "-0.25"), Is.EqualTo(-0.25).And.TypeOf<double>()); }
        [Test] public void Parse_DoubleBigButFinite() { Assert.That(Parse(typeof(double), "1e39"), Is.EqualTo(1e39)); }
        [Test]
        public void Parse_NaNAndInfinity_AreNull()
        {
            foreach (var s in new[] { "NaN", "Infinity", "-Infinity", "1e999" }) Assert.That(Parse(typeof(double), s), Is.Null, s);
        }
        [Test]
        public void Parse_Garbage_IsNull()
        {
            foreach (var s in new[] { "", " ", "abc", "1.2.3", "--1", "1 2", "0x10" }) Assert.That(Parse(typeof(double), s), Is.Null, "'" + s + "'");
        }
        [Test] public void Parse_Null_IsNull() { Assert.That(Parse(typeof(int), null), Is.Null); }
        [Test] public void Parse_NotANumberType_IsNull() { Assert.That(Parse(typeof(string), "5"), Is.Null); }
        [Test]
        public void Parse_DoesNotUseTheCurrentCulture()
        {
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.That(Parse(typeof(double), "1.5"), Is.EqualTo(1.5));
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = old; }
        }

        // --- Clamp ------------------------------------------------------------------------------------------------------------
        [Test] public void Clamp_InsideRange_Stays() { Assert.That(Clamp(typeof(float), 2.5, 0, 5), Is.EqualTo(2.5f).And.TypeOf<float>()); }
        [Test] public void Clamp_Below_GoesToMin() { Assert.That(Clamp(typeof(float), -3, 0, 5), Is.EqualTo(0f)); }
        [Test] public void Clamp_Above_GoesToMax() { Assert.That(Clamp(typeof(double), 9, 0, 5), Is.EqualTo(5.0).And.TypeOf<double>()); }
        [Test] public void Clamp_IntRoundsToNearest() { Assert.That(Clamp(typeof(int), 2.6, 0, 5), Is.EqualTo(3).And.TypeOf<int>()); }
        [Test] public void Clamp_IntRoundsDown() { Assert.That(Clamp(typeof(int), 2.4, 0, 5), Is.EqualTo(2)); }
        [Test] public void Clamp_Long() { Assert.That(Clamp(typeof(long), 7.2, 0, 10), Is.EqualTo(7L).And.TypeOf<long>()); }
        [Test] public void Clamp_NegativeRange() { Assert.That(Clamp(typeof(int), -20, -10, -2), Is.EqualTo(-10)); }

        [Test]
        public void Property_Clamp_AlwaysInsideTheRange()
        {
            var ranges = from a in Gen.Choose(-1000, 1000) from b in Gen.Choose(0, 1000) select new[] { a, a + b };
            Props.Check(Prop.ForAll(ranges.ToArbitrary(), Props.GameFloat(5000f).ToArbitrary(), (r, v) =>
            {
                var i = (int)Clamp(typeof(int), v, r[0], r[1]);
                var f = (float)Clamp(typeof(float), v, r[0], r[1]);
                return (i >= r[0] && i <= r[1]) && (f >= r[0] && f <= r[1]);
            }));
        }

        [Test]
        public void Property_Int_FormatThenParse_IsTheSameNumber()
        {
            Props.Check(Prop.ForAll(Arb.Default.Int32(), n => Equals(Parse(typeof(int), Format(typeof(int), n)), n)));
        }

        [Test]
        public void Property_Long_FormatThenParse_IsTheSameNumber()
        {
            Props.Check(Prop.ForAll(Arb.Default.Int64(), n => Equals(Parse(typeof(long), Format(typeof(long), n)), n)));
        }

        [Test]
        public void Property_Double_ThreeDecimals_RoundTripThroughText()
        {
            // numbers with at most 2 decimals are shown exactly, and what is shown reads back to the same number
            Props.Check(Prop.ForAll(Gen.Choose(-1000000, 1000000).Select(i => i / 100.0).ToArbitrary(), d =>
                Math.Abs((double)Parse(typeof(double), Format(typeof(double), d)) - d) < 1e-9));
        }

        [Test]
        public void Property_Parse_NeverThrows_OnAnyText()
        {
            Props.Check(Prop.ForAll(Arb.Default.String(), s =>
            {
                foreach (var t in new[] { typeof(int), typeof(long), typeof(float), typeof(double) }) Parse(t, s);
                return true;
            }));
        }

        // --- tags -------------------------------------------------------------------------------------------------------------
        [Test] public void Tags_None_NothingIsHiddenOrAdminOnly() { Assert.That(Hidden(), Is.False); Assert.That(Admin(), Is.False); }
        [Test]
        public void Tags_Null_NothingIsHiddenOrAdminOnly()
        {
            Assert.That(Call("IsHidden", new object[] { null }), Is.False);
            Assert.That(Call("IsAdminOnly", new object[] { null }), Is.False);
        }
        [Test] public void Tags_NullTag_IsSkipped() { Assert.That(Hidden(null, new FieldTag { Browsable = false }), Is.True); }
        [Test] public void Tags_FieldBrowsableFalse_IsHidden() { Assert.That(Hidden(new FieldTag { Browsable = false }), Is.True); }
        [Test] public void Tags_FieldBrowsableTrue_IsShown() { Assert.That(Hidden(new FieldTag { Browsable = true }), Is.False); }
        [Test] public void Tags_PropertyBrowsableFalse_IsHidden() { Assert.That(Hidden(new PropertyTag { Browsable = false }), Is.True); }
        [Test] public void Tags_PropertyBrowsableTrue_IsShown() { Assert.That(Hidden(new PropertyTag { Browsable = true }), Is.False); }
        [Test] public void Tags_NullableBrowsableFalse_IsHidden() { Assert.That(Hidden(new NullableTag { Browsable = false }), Is.True); }
        [Test] public void Tags_NullableBrowsableUnset_IsShown() { Assert.That(Hidden(new NullableTag()), Is.False); }
        [Test] public void Tags_PrivateMember_IsFound() { Assert.That(Hidden(new PrivateTag()), Is.True); Assert.That(Admin(new PrivateTag()), Is.True); }
        [Test] public void Tags_WrongTypedMember_IsIgnored() { Assert.That(Hidden(new WrongTypeTag()), Is.False); Assert.That(Admin(new WrongTypeTag()), Is.False); }
        [Test] public void Tags_UnrelatedTag_IsIgnored() { Assert.That(Hidden(new UnrelatedTag()), Is.False); Assert.That(Admin(new UnrelatedTag(), "text", 5), Is.False); }
        [Test] public void Tags_StringAndIndexerTags_DoNotThrow() { Assert.That(Hidden("a string", new IndexerTag()), Is.False); }
        [Test]
        public void Tags_AdminOnlyTrue_FieldAndProperty()
        {
            Assert.That(Admin(new FieldTag { IsAdminOnly = true }), Is.True);
            Assert.That(Admin(new PropertyTag { IsAdminOnly = true }), Is.True);
            Assert.That(Admin(new NullableTag { IsAdminOnly = true }), Is.True);
        }
        [Test] public void Tags_AdminOnlyFalse() { Assert.That(Admin(new FieldTag()), Is.False); Assert.That(Admin(new PropertyTag()), Is.False); }
        [Test] public void Tags_AdminOnlyDoesNotHide() { Assert.That(Hidden(new FieldTag { IsAdminOnly = true }), Is.False); }
        [Test] public void Tags_HiddenIsNotAdminOnly() { Assert.That(Admin(new FieldTag { Browsable = false }), Is.False); }
        [Test]
        public void Tags_FirstTagWithTheMemberWins()
        {
            Assert.That(Hidden(new UnrelatedTag(), new FieldTag { Browsable = false }, new FieldTag { Browsable = true }), Is.True);
            Assert.That(Hidden(new FieldTag { Browsable = true }, new FieldTag { Browsable = false }), Is.False);
            Assert.That(Hidden(new NullableTag(), new FieldTag { Browsable = false }), Is.True, "an unset bool? does not stop the search");
        }
    }
}
