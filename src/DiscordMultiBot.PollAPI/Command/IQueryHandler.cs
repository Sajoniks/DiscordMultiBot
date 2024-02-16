using DiscordMultiBot.PollService.Data.Dto;
using DiscordMultiBot.PollService.Data.Entity;

namespace DiscordMultiBot.PollService.Command;

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery
{
    Task<ResultDto<TResult>> AskAsync(TQuery query);
}