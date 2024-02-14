using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.PollService.Command;

public class CommandDispatcher
{
    private readonly IServiceProvider _provider;

    public CommandDispatcher(IServiceProvider provider)
    {
        _provider = provider;
    }

    public Task<TResult> ExecuteAsync<TCommand, TResult>(TCommand command) where TCommand : ICommand
    {
        var handler = _provider.GetRequiredService<ICommandHandler<TCommand, TResult>>();
        return handler.Execute(command);
    }

    public Task ExecuteAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        var handler = _provider.GetRequiredService<ICommandHandler<TCommand>>();
        return handler.Execute(command);
    }
}