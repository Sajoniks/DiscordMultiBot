using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Command;


public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    public Task<ResultDto<TResult>> AskAsync(TQuery query);
}