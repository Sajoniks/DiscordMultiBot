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

    public Task<ResultDto<TResult>> ExecuteAsync<TCommand, TResult>(TCommand command) where TCommand : ICommand
    {
        var handler = _provider.GetRequiredService<ICommandHandler<TCommand, TResult>>();
        return handler.ExecuteAsync(command);
    }

    public Task<ResultDto> ExecuteAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        var handler = _provider.GetRequiredService<ICommandHandler<TCommand>>();
        return handler.ExecuteAsync(command);
    }

    public Task<ResultDto<TResult>> QueryAsync<TQuery, TResult>(TQuery query) where TQuery : IQuery
    {
        var handler = _provider.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return handler.AskAsync(query);
    }
}