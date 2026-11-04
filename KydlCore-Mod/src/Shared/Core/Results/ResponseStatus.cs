namespace KydlCore.Core;

/// <summary>
/// Kydl.Core (Architecture V2, SteveTheTroll D-169): how an operation ended. A plain bool cannot tell "not my business" from "refused" from "broke", which is why every decision and command answers
/// with one of these four.
/// </summary>
public enum ResponseStatus
{
    /// The operation was decided / done as asked.
    Success,
    /// The operation applies, but a rule refused it (not the owner, backpack full, target protected).
    Rejected,
    /// The operation does not apply right now (nothing to do, wrong state); not an error.
    Ignored,
    /// Something broke (missing handler, exception, invalid input).
    Failed,
}
