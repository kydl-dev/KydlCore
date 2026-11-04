namespace KydlCore.Features.Hub.Domain;

/// <summary>T-098: the game-free decision of the page host (unit-tested).</summary>
internal static class PageRules
{
    /// <summary>Whether the content on screen must be replaced: nothing shown yet, an empty State (always rebuild), or a different State.</summary>
    internal static bool NeedsRebuild(string shown, string next)
    {
        if (shown == null) return true;
        if (string.IsNullOrEmpty(next)) return true;
        return !string.Equals(shown, next, System.StringComparison.Ordinal);
    }
}
