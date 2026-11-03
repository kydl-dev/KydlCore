using System.IO;
using BepInEx;

namespace KydlCore.Common;

/// <summary>
/// Files a mod writes for itself that are not config (dumps, generated data, caches) go to BepInEx/cache/&lt;mod name&gt;/ (user, 2026-10-27;
/// PROJECT_RULES 18). Never into BepInEx/config (that folder is for .cfg files the player edits) or next to the DLL.
/// </summary>
public static class ModCache
{
    /// Expected outcome: the full path of BepInEx/cache/&lt;modName&gt;/; the folder exists after the call.
    public static string Dir(string modName)
    {
        string dir = Path.Combine(Paths.CachePath, modName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// Expected outcome: the full path of BepInEx/cache/&lt;modName&gt;/&lt;fileName&gt; (folder created, file not touched).
    public static string File(string modName, string fileName) { return Path.Combine(Dir(modName), fileName); }
}