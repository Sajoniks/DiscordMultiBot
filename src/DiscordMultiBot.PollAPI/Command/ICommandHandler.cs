namespace DiscordMultiBot.PollService.Command;

public interface ICommandHandler
{
    
}

public interface ICommandHandler<TCommand> : ICommandHandler where TCommand : ICommand
{
    Task Execute(TCommand command);
}

public interface ICommandHandler<TCommand, TResult> : ICommandHandler where TCommand : ICommand
{
    Task<TResult> Execute(TCommand command);
}