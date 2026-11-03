namespace KydlCore.Core;

/// <summary>Kydl.Core (Architecture V2, SteveTheTroll D-169): handles one command type (validate, decide, hand the decision to an executor) and says how it ended.</summary>
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    OperationResponse<TResult> Handle(TCommand command);
}