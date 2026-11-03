using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace KydlCore.Tests.Unit
{
    /// <summary>
    /// KeyCaptureRules (D-075): what a frame of key presses means for the press-to-rebind button of the Settings page, and how a key / shortcut is
    /// written on it. Reached in the BUILT mod DLL by reflection (like ConfigControlMap) so Stryker can mutate it.
    /// </summary>
    [TestFixture]
    public class KeyCaptureRulesTests
    {
        static readonly Type Rules = ModAssembly.Type("KydlCore.Ui.KeyCaptureRules");

        // returns "Step main [mods]" so one assertion shows the whole decision
        static string Decide(KeyCode[] pressed, KeyCode[] held, bool withModifiers)
        {
            var args = new object[] { new List<KeyCode>(pressed), new List<KeyCode>(held), withModifiers, KeyCode.None, null };
            object step = Rules.GetMethod("Decide").Invoke(null, args);
            var mods = (List<KeyCode>)args[4];
            return step + " " + args[3] + " [" + string.Join(",", mods.ConvertAll(m => m.ToString()).ToArray()) + "]";
        }

        static string Name(KeyCode k) { return (string)Rules.GetMethod("KeyName").Invoke(null, new object[] { k }); }
        static string Text(KeyCode main, params KeyCode[] mods) { return (string)Rules.GetMethod("ShortcutText").Invoke(null, new object[] { main, new List<KeyCode>(mods) }); }
        static bool IsMod(KeyCode k) { return (bool)Rules.GetMethod("IsModifier").Invoke(null, new object[] { k }); }

        [TestCase(KeyCode.LeftShift)] [TestCase(KeyCode.RightShift)] [TestCase(KeyCode.LeftControl)]
        [TestCase(KeyCode.RightControl)] [TestCase(KeyCode.LeftAlt)] [TestCase(KeyCode.RightAlt)]
        public void IsModifier_ShiftControlAlt(KeyCode k) { Assert.That(IsMod(k), Is.True); }

        [TestCase(KeyCode.K)] [TestCase(KeyCode.F6)] [TestCase(KeyCode.Space)] [TestCase(KeyCode.None)] [TestCase(KeyCode.Mouse3)]
        public void IsModifier_OtherKeysNo(KeyCode k) { Assert.That(IsMod(k), Is.False); }

        [Test] public void Nothing_Waits() { Assert.That(Decide(new KeyCode[0], new KeyCode[0], false), Is.EqualTo("Waiting None []")); }

        [Test] public void NullPressed_Waits()
        {
            var args = new object[] { null, null, false, KeyCode.None, null };
            Assert.That(Rules.GetMethod("Decide").Invoke(null, args).ToString(), Is.EqualTo("Waiting"));
        }

        [Test] public void Escape_Cancels() { Assert.That(Decide(new[] { KeyCode.Escape }, new KeyCode[0], false), Is.EqualTo("Cancelled None []")); }
        [Test] public void Escape_BeatsOtherKeysInTheSameFrame() { Assert.That(Decide(new[] { KeyCode.K, KeyCode.Escape }, new KeyCode[0], true), Is.EqualTo("Cancelled None []")); }

        [Test] public void SingleKey_IsTaken() { Assert.That(Decide(new[] { KeyCode.F9 }, new KeyCode[0], false), Is.EqualTo("Done F9 []")); }
        [Test] public void SingleKey_ModifierCounts() { Assert.That(Decide(new[] { KeyCode.LeftAlt }, new[] { KeyCode.LeftAlt }, false), Is.EqualTo("Done LeftAlt []")); }
        [Test] public void SingleKey_IgnoresHeldModifiers() { Assert.That(Decide(new[] { KeyCode.K }, new[] { KeyCode.LeftShift }, false), Is.EqualTo("Done K []")); }
        [Test] public void MouseButton_IsAKey() { Assert.That(Decide(new[] { KeyCode.Mouse3 }, new KeyCode[0], false), Is.EqualTo("Done Mouse3 []")); }
        [Test] public void LeftClick_NeverCounts() { Assert.That(Decide(new[] { KeyCode.Mouse0 }, new KeyCode[0], false), Is.EqualTo("Waiting None []")); }
        [Test] public void LeftClick_IsSkippedForTheNextKey() { Assert.That(Decide(new[] { KeyCode.Mouse0, KeyCode.G }, new KeyCode[0], false), Is.EqualTo("Done G []")); }
        [Test] public void NoneKey_NeverCounts() { Assert.That(Decide(new[] { KeyCode.None }, new KeyCode[0], false), Is.EqualTo("Waiting None []")); }

        [Test] public void Shortcut_KeyAlone() { Assert.That(Decide(new[] { KeyCode.K }, new KeyCode[0], true), Is.EqualTo("Done K []")); }
        [Test] public void Shortcut_ModifierAloneWaits() { Assert.That(Decide(new[] { KeyCode.LeftControl }, new[] { KeyCode.LeftControl }, true), Is.EqualTo("Waiting None []")); }
        [Test] public void Shortcut_TakesHeldModifiers() { Assert.That(Decide(new[] { KeyCode.K }, new[] { KeyCode.LeftControl, KeyCode.LeftShift }, true), Is.EqualTo("Done K [LeftControl,LeftShift]")); }
        [Test] public void Shortcut_KeepsTheModifierSideThatWasHeld() { Assert.That(Decide(new[] { KeyCode.K }, new[] { KeyCode.RightShift }, true), Is.EqualTo("Done K [RightShift]")); }
        [Test] public void Shortcut_ModifierAndKeyInOneFrame() { Assert.That(Decide(new[] { KeyCode.LeftShift, KeyCode.K }, new[] { KeyCode.LeftShift }, true), Is.EqualTo("Done K [LeftShift]")); }
        [Test] public void Shortcut_NonModifierHeldIsNotAModifier() { Assert.That(Decide(new[] { KeyCode.K }, new[] { KeyCode.G }, true), Is.EqualTo("Done K []")); }
        [Test] public void Shortcut_ModifiersListedOnce() { Assert.That(Decide(new[] { KeyCode.K }, new[] { KeyCode.LeftAlt, KeyCode.LeftAlt }, true), Is.EqualTo("Done K [LeftAlt]")); }
        [Test] public void Shortcut_NullHeld_NoModifiers()
        {
            var args = new object[] { new List<KeyCode> { KeyCode.K }, null, true, KeyCode.None, null };
            Assert.That(Rules.GetMethod("Decide").Invoke(null, args).ToString(), Is.EqualTo("Done"));
            Assert.That(((List<KeyCode>)args[4]).Count, Is.EqualTo(0));
        }

        [Test] public void KeyName_None() { Assert.That(Name(KeyCode.None), Is.EqualTo("(none)")); }
        [TestCase(KeyCode.Alpha0, "0")] [TestCase(KeyCode.Alpha5, "5")] [TestCase(KeyCode.Alpha9, "9")]
        public void KeyName_DigitRow(KeyCode k, string expected) { Assert.That(Name(k), Is.EqualTo(expected)); }
        [TestCase(KeyCode.F6, "F6")] [TestCase(KeyCode.K, "K")] [TestCase(KeyCode.Mouse3, "Mouse3")] [TestCase(KeyCode.Keypad1, "Keypad1")]
        public void KeyName_OtherKeysByName(KeyCode k, string expected) { Assert.That(Name(k), Is.EqualTo(expected)); }

        [Test] public void ShortcutText_KeyOnly() { Assert.That(Text(KeyCode.K), Is.EqualTo("K")); }
        [Test] public void ShortcutText_ModifiersFirst() { Assert.That(Text(KeyCode.K, KeyCode.LeftControl, KeyCode.LeftShift), Is.EqualTo("LeftControl + LeftShift + K")); }
        [Test] public void ShortcutText_NoMainKey_IsNone() { Assert.That(Text(KeyCode.None, KeyCode.LeftShift), Is.EqualTo("(none)")); }
        [Test] public void ShortcutText_NullModifiers()
        {
            Assert.That((string)Rules.GetMethod("ShortcutText").Invoke(null, new object[] { KeyCode.Alpha3, null }), Is.EqualTo("3"));
        }
    }
}
