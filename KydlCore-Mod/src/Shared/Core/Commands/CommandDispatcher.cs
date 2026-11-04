using System;
using System.Collections.Generic;

namespace KydlCore.Core;

/// <summary>
/// Kydl.Core (Architecture V2, SteveTheTroll D-169): routes a command to the one handler registered for its type. No message bus, no queue, no events: a request is handled at once on the caller's
/// thread and the caller gets the typed response. A missing handler or a throwing handler is a Failed response (never an exception for the caller). Game-free; the optional
/// trace sink (ModLog.Verbose in game) gets one line per dispatch.
/// </summary>
public sealed class CommandDispatcher
{
    readonly Dictionary<Type, object> _handlers = new();
    readonly Action<string> _trace;

    public CommandDispatcher(Action<string> trace = null) { _trace = trace; }

    /// How many command types have a handler.
    public int Count => _handlers.Count;

    /// Registers (or replaces) the handler of TCommand. Expected outcome: a handler is registered for TCommand.
    public bool Register<TCommand, TResult>(ICommandHandler<TCommand, TResult> handler) where TCommand : ICommand<TResult>
    {
        if (handler == null) throw new ArgumentNullException("handler");
        Func<ICommand<TResult>, OperationResponse<TResult>> route = c => handler.Handle((TCommand)c);
        bool replaced = _handlers.ContainsKey(typeof(TCommand));
        _handlers[typeof(TCommand)] = route;
        Trace("CommandDispatcher: " + (replaced ? "replaced" : "registered") + " the handler of " + typeof(TCommand).Name + " (" + _handlers.Count + " command types)");
        return Handles(typeof(TCommand));
    }

    public bool Handles(Type commandType) { return commandType != null && _handlers.ContainsKey(commandType); }

    /// Handles the command. Expected outcome: the handler's response; Failed for a null command, a command type without a handler, or a handler that threw.
    public OperationResponse<TResult> Dispatch<TResult>(ICommand<TResult> command)
    {
        if (command == null) return Done<TResult>("null", OperationResponse<TResult>.Failed("no command given"));
        var type = command.GetType();
        object entry;
        if (!_handlers.TryGetValue(type, out entry)) return Done(type.Name, OperationResponse<TResult>.Failed("no handler registered for " + type.Name));
        var route = entry as Func<ICommand<TResult>, OperationResponse<TResult>>;
        if (route == null) return Done(type.Name, OperationResponse<TResult>.Failed("the handler of " + type.Name + " answers another result type"));
        try
        {
            return Done(type.Name, route(command));
        }
        catch (InvalidCastException e)
        {
            return Done(type.Name, OperationResponse<TResult>.Failed("handler of " + type.Name + " got the wrong command type: " + e.Message));
        }
        catch (Exception e)       // last resort: a handler bug must not escape into the game loop; the reason names the exception
        {
            return Done(type.Name, OperationResponse<TResult>.Failed("handler of " + type.Name + " threw " + e.GetType().Name + ": " + e.Message));
        }
    }

    OperationResponse<TResult> Done<TResult>(string command, OperationResponse<TResult> response)
    {
        Trace("CommandDispatcher: " + command + " -> " + response);
        return response;
    }

    void Trace(string line) { if (_trace != null) _trace(line); }
}
