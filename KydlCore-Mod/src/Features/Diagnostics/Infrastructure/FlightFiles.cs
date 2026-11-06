using System;
using System.IO;
using System.Linq;
using System.Text;
using KydlCore.Common;
using KydlCore.Features.Diagnostics.Domain;

namespace KydlCore.Features.Diagnostics.Infrastructure;

/// <summary>
/// Where the flight recorder's dumps go: BepInEx\cache\KydlCore\flight\&lt;yyyyMMdd-HHmmss-fff&gt;_&lt;reason&gt;.txt (PROJECT_RULES 18: files a
/// mod writes for itself go to its cache folder). Kydl.GameProbe copies the dumps of a session into its session folder. Old dumps are
/// removed at game start (older than the kept days, or more than the kept count).
/// </summary>
internal static class FlightFiles
{
    const string CacheOwner = "KydlCore";
    const string Folder = "flight";

    /// <summary>The dump folder (created when missing).</summary>
    internal static string Dir()
    {
        string dir = Path.Combine(ModCache.Dir(CacheOwner), Folder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Writes one dump.</summary>
    /// <returns>The file's path, or null when it could not be written (the reason is logged).</returns>
    internal static string Write(string reason, string text)
    {
        string path = Path.Combine(Dir(), DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "_" + TriggerRules.Slug(reason) + ".txt");
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        catch (IOException e)
        {
            ModLog.Warn("FlightRecorder: could not write " + path + ": " + e.Message);
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            ModLog.Warn("FlightRecorder: no permission to write " + path + ": " + e.Message);
            return null;
        }

        return File.Exists(path) ? path : null;
    }

    /// <summary>Removes dumps older than <paramref name="keepDays"/> days and the oldest beyond <paramref name="keepMax"/> files.</summary>
    /// <returns>How many files were removed.</returns>
    internal static int Prune(int keepDays, int keepMax)
    {
        int removed = 0;
        try
        {
            var files = new DirectoryInfo(Dir()).GetFiles("*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
            DateTime cutoff = DateTime.Now.AddDays(-Math.Max(1, keepDays));
            for (int i = 0; i < files.Count; i++)
            {
                if (i < keepMax && files[i].LastWriteTime >= cutoff)
                {
                    continue;
                }

                files[i].Delete();
                removed++;
            }
        }
        catch (IOException e)
        {
            ModLog.Warn("FlightRecorder: old dumps not removed: " + e.Message);
        }
        catch (UnauthorizedAccessException e)
        {
            ModLog.Warn("FlightRecorder: no permission to remove old dumps: " + e.Message);
        }

        return removed;
    }
}
