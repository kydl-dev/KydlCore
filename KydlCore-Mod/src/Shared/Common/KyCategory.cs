using System.Reflection;
using System.Runtime.CompilerServices;

namespace KydlCore.Common;

/// <summary>
/// SHARED. The build menu category of every buildable piece of the user's mods (user, 2026-10-21: "KY-Items" for all current and future
/// items of my mods). Jotunn makes one custom category per NAME, so every mod that uses exactly this string ends up in the same entry of the
/// hammer's build menu. Never change the string.
/// </summary>
public static class KyCategory
{
    public const string Name = "KY-Items";

    /// The category for a new piece (logged once per piece, so a piece that ends up elsewhere can be traced).
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string For(string piece)
    {
        ModLog.Verbose(ModContexts.For(Assembly.GetCallingAssembly()), "KyCategory: piece '" + piece + "' goes into the build menu category '" + Name + "'");
        return Name;
    }
}