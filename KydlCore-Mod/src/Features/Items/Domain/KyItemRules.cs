using System;
using KydlCore.Core;

namespace KydlCore.Features.Items.Domain;

/// <summary>
/// The rules of a KY-Items contribution id (spec 5, MIGRATION_DECISIONS B13): "<modid>:<item>", lowercase, where <modid> is the owning mod's GUID
/// after "com.kydldev." ("com.kydldev.SteveTheTroll" -> "stevethetroll"), e.g. "stevethetroll:slippers". Pure: tested in tests/Unit.
/// </summary>
internal static class KyItemRules
{
    internal const string GuidPrefix = "com.kydldev.";
    internal const string NoId = "no id", NoMod = "no owning mod GUID", NotUserMod = "the owner is not a KydlDev mod (com.kydldev.*)",
                          BadShape = "the id must look like '<modid>:<item>'", BadChars = "the item part may only use a-z, 0-9, '_', '-' and '.'";

    /// True for "com.kydldev.*" GUIDs with a name after the prefix (case ignored).
    internal static bool IsUserGuid(string guid)
    {
        return guid != null && guid.Length > GuidPrefix.Length && guid.StartsWith(GuidPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// The id namespace of a mod: its GUID after the prefix, lowercase ("" for a GUID that is not the user's).
    internal static string ModId(string guid) { return IsUserGuid(guid) ? guid.Substring(GuidPrefix.Length).ToLowerInvariant() : ""; }

    /// Expected outcome: Success with the id in lowercase; Rejected (with the reason) for a missing / foreign owner, a wrong shape, another
    /// mod's namespace or characters outside a-z 0-9 _ - . in the item part.
    internal static OperationResponse<string> ValidateId(string id, string modGuid)
    {
        if (string.IsNullOrEmpty(id)) return OperationResponse<string>.Rejected(NoId);
        if (string.IsNullOrEmpty(modGuid)) return OperationResponse<string>.Rejected(NoMod);
        if (!IsUserGuid(modGuid)) return OperationResponse<string>.Rejected(NotUserMod);
        string lower = id.Trim().ToLowerInvariant();
        int colon = lower.IndexOf(':');
        if (colon <= 0 || colon == lower.Length - 1 || lower.IndexOf(':', colon + 1) >= 0) return OperationResponse<string>.Rejected(BadShape);
        string ns = lower.Substring(0, colon), item = lower.Substring(colon + 1);
        string expected = ModId(modGuid);
        if (ns != expected) return OperationResponse<string>.Rejected("the id namespace '" + ns + "' is not the owning mod '" + expected + "'");
        foreach (char ch in item)
            if (!((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-' || ch == '.')) return OperationResponse<string>.Rejected(BadChars);
        return OperationResponse<string>.Success(lower);
    }
}
