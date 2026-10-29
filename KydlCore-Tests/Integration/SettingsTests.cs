using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace KydlCore.Tests.Integration
{
    /// <summary>
    /// User rule: the player config holds only what a player should change, everything else is a Tunable (dev-only, shown by KydlTools).
    /// KydlTools finds Tunables by reflection (type name "Tunable`1", properties Section / Name / Description / Default / Value / External):
    /// these tests keep that contract from breaking.
    /// </summary>
    [TestFixture]
    public class SettingsTests
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static List<object> Tunables()
        {
            var list = new List<object>();
            foreach (var f in ModAssembly.Type("KydlCore.ModSettings").GetFields(Static))
                if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition().Name == "Tunable`1") list.Add(f.GetValue(null));
            return list;
        }

        [Test]
        public void Tunables_HaveTheMembersKydlToolsReads()
        {
            foreach (var t in Tunables())
                foreach (var p in new[] { "Section", "Name", "Description", "Default", "Value", "External" })
                    Assert.That(t.GetType().GetProperty(p), Is.Not.Null, "Tunable property " + p + " missing (KydlTools reads it)");
        }

        [Test]
        public void Tunables_StartAtTheirFixedValue()
        {
            foreach (var t in Tunables())
            {
                var ty = t.GetType();
                Assert.That(ty.GetProperty("Value").GetValue(t, null), Is.EqualTo(ty.GetProperty("Default").GetValue(t, null)), t.ToString());
            }
        }

        [Test]
        public void PlayerConfig_HasDebugLogSwitch()
        {
            var src = File.ReadAllText(Path.Combine(TestPaths.ModDir, "src", "ModSettings.cs"));

            Assert.That(Regex.IsMatch(src, @"Bind\(\s*""DebugLog""\s*,\s*""Enable""\s*,\s*false"), Is.True, "[DebugLog] Enable (off by default) is required in every mod");
        }

        [Test]
        public void ConfigDescriptions_DoNotTalkAboutOldVersions()
        {
            var src = File.ReadAllText(Path.Combine(TestPaths.ModDir, "src", "ModSettings.cs"));

            foreach (Match m in Regex.Matches(src, @"Bind\([^;]*?""(?<d>[^""]{20,})"""))
                Assert.That(Regex.IsMatch(m.Groups["d"].Value, @"\b(previous|formerly|renamed|old value|used to|before version)\b", RegexOptions.IgnoreCase), Is.False, m.Groups["d"].Value);
        }
    }
}
