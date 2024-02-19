using System.Reflection;
using DiscordMultiBot.PollService.Data.Dto;
using DiscordMultiBot.PollService.Data.Entity;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.PollService.Command;

public class CommandDispatcher
{
    private readonly IServiceProvider _provider;

    public CommandDispatcher(IServiceProvider provider)
    {
        _provider = provider;
    }

    public Task<ResultDto<TResult>> ExecuteAsync<TResult>(ICommand<TResult> command)
    {
        var commandType = command.GetType();
        var resultType = typeof(TResult);
        
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, resultType);
        var handler = _provider.GetRequiredService(handlerType);
        var r = handlerType.GetMethod("ExecuteAsync")!.Invoke(handler, new object?[] { command })!;
        return (Task<ResultDto<TResult>>)r;
    }

    public Task<ResultDto> ExecuteAsync(ICommand command)
    {
        var commandType = command.GetType();
        
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
        var handler = _provider.GetRequiredService(handlerType);
        var r = handlerType.GetMethod("ExecuteAsync")!.Invoke(handler, new object?[] { command })!;
        return (Task<ResultDto>)r;
    }

    public Task<ResultDto<TResult>> QueryAsync<TResult>(IQuery<TResult> query)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = _provider.GetRequiredService(handlerType);
        var r = handlerType.GetMethod("AskAsync")!.Invoke(handler, new object?[] { query })!;
        return (Task<ResultDto<TResult>>)r;
    }
}