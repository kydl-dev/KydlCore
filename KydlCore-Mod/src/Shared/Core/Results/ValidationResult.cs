namespace KydlCore.Core;

/// <summary>
/// Kydl.Core (Architecture V2, SteveTheTroll D-169): the answer of one validation step of a pipeline. Valid lets the pipeline go on; otherwise it stops with Rejected (a rule refused) or Ignored
/// (does not apply now). Readonly struct: no allocation per check.
/// </summary>
public readonly struct ValidationResult
{
    /// Success = valid; Rejected / Ignored = the pipeline stops with this status.
    public ResponseStatus Status { get; }
    public string Reason { get; }

    ValidationResult(ResponseStatus status, string reason)
    {
        Status = status;
        Reason = reason ?? "";
    }

    public bool IsValid => Status == ResponseStatus.Success;

    public static readonly ValidationResult Valid = new(ResponseStatus.Success, "");
    public static ValidationResult Reject(string reason) { return new ValidationResult(ResponseStatus.Rejected, reason); }
    public static ValidationResult Ignore(string reason) { return new ValidationResult(ResponseStatus.Ignored, reason); }

    /// Valid when `condition` holds, else Rejected with `reason` (no allocation when the reason is a constant).
    public static ValidationResult RejectUnless(bool condition, string reason) { return condition ? Valid : Reject(reason); }
    /// Valid when `condition` holds, else Ignored with `reason`.
    public static ValidationResult IgnoreUnless(bool condition, string reason) { return condition ? Valid : Ignore(reason); }

    public override string ToString() { return IsValid ? "Valid" : Status + ": " + Reason; }
}