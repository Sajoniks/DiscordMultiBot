using System.Reflection;
using Discord.Interactions;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.App.Commands;

public interface IBotCommand<TContext> : ICommand { };
public interface IBotCommand<TContext, TResult> : ICommand<TResult> { };

public interface ISocketBotCommand : IBotCommand<SocketInteractionContext> { }
public interface ISocketBotCommand<TResult> : IBotCommand<SocketInteractionContext, TResult> { }

public interface IBotCommandHandler<TCommand, TContext> where TCommand : IBotCommand<TContext>
{
    Task<ResultDto> ExecuteAsync(TContext context, CommandDispatcher dispatcher, TCommand command);
}
public interface IBotCommandHandler<TCommand, TResult, TContext> where TCommand : IBotCommand<TContext, TResult>
{
    Task<ResultDto<TResult>> ExecuteAsync(TContext context, CommandDispatcher dispatcher, TCommand command);
}

public interface ISocketBotCommandHandler<TCommand> : IBotCommandHandler<TCommand, SocketInteractionContext>
    where TCommand : ISocketBotCommand
{ }

public interface ISocketBotCommandHandler<TCommand, TResult> : IBotCommandHandler<TCommand, TResult, SocketInteractionContext>
    where TCommand : ISocketBotCommand<TResult>
{ }

public class BotCommandDispatcher
{
    private readonly CommandDispatcher _baseDispatcher;
    private readonly IServiceProvider _services;

    public BotCommandDispatcher(IServiceProvider services)
    {
        _baseDispatcher = services.GetRequiredService<CommandDispatcher>();
        _services = services;
    }

    public Task<ResultDto> ExecuteAsync(SocketInteractionContext context,
        ISocketBotCommand command)
    {
        var baseHandlerType =
            typeof(IBotCommandHandler<,>).MakeGenericType(command.GetType(), typeof(SocketInteractionContext));
        var requiredHandlerType = typeof(ISocketBotCommandHandler<>).MakeGenericType(command.GetType());
        
        var handler = _services.GetRequiredService(requiredHandlerType);
        
        var r = baseHandlerType.GetMethod("ExecuteAsync")!.Invoke(handler,
            new object?[] { context, _baseDispatcher, command })!;
        return (Task<ResultDto>)r;
    }
    
    public Task<ResultDto<TResult>> ExecuteAsync<TResult>(SocketInteractionContext context,
        ISocketBotCommand<TResult> command)
    {
        var baseHandlerType =
            typeof(IBotCommandHandler<,,>).MakeGenericType(command.GetType(), typeof(TResult), typeof(SocketInteractionContext));
        var requiredHandlerType = typeof(ISocketBotCommandHandler<>).MakeGenericType(command.GetType());

        var handler = _services.GetRequiredService(requiredHandlerType);
        
        var r = baseHandlerType.GetMethod("ExecuteAsync")!.Invoke(handler, new object?[] { context, _baseDispatcher, command })!;
        return (Task<ResultDto<TResult>>)r;
    }
}