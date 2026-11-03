namespace KydlCore.Core;

/// <summary>
/// Kydl.Core (Architecture V2, SteveTheTroll D-169): the typed answer of a decision or command - a status, the typed value (meaningful on Success, default otherwise) and a reason for the log.
/// A readonly struct so per-frame decisions (recovery, farming) allocate nothing; game-free (no Unity / Valheim types), so every rule that returns one is unit-testable.
/// </summary>
public readonly struct OperationResponse<T>
{
    public ResponseStatus Status { get; }
    public T Value { get; }
    /// Why it ended like this (constant strings in hot paths; never null).
    public string Reason { get; }

    public OperationResponse(ResponseStatus status, T value, string reason)
    {
        Status = status;
        Value = value;
        Reason = reason ?? "";
    }

    public bool IsSuccess => Status == ResponseStatus.Success;

    public static OperationResponse<T> Success(T value, string reason = "") { return new OperationResponse<T>(ResponseStatus.Success, value, reason); }
    public static OperationResponse<T> Rejected(string reason) { return new OperationResponse<T>(ResponseStatus.Rejected, default(T), reason); }
    public static OperationResponse<T> Ignored(string reason) { return new OperationResponse<T>(ResponseStatus.Ignored, default(T), reason); }
    public static OperationResponse<T> Failed(string reason) { return new OperationResponse<T>(ResponseStatus.Failed, default(T), reason); }

    /// The same outcome with another value type (a rejected validation passed on by a pipeline). Expected outcome: same status and reason, default value.
    public OperationResponse<TOther> As<TOther>() { return new OperationResponse<TOther>(Status, default(TOther), Reason); }

    public override string ToString() { return Reason.Length == 0 ? Status.ToString() : Status + ": " + Reason; }
}