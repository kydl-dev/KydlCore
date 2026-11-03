namespace KydlCore.Core;

/// <summary>Kydl.Core (Architecture V2, SteveTheTroll D-169): the deciding step of a pipeline: facts in, typed decision out. Pure: no side effects, no Unity / Valheim calls.</summary>
public interface IOperationStep<TInput, TOutput>
{
    OperationResponse<TOutput> Run(TInput input);
}