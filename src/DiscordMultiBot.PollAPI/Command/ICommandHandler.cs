using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Command;

public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<ResultDto> ExecuteAsync(TCommand command);
}

public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<ResultDto<TResult>> ExecuteAsync(TCommand command);
}