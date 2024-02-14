using DiscordMultiBot.PollService.Data;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Repository;


public interface IPollRepository : IRepository<PollDto>
{
    public Task<PollDto?> CreatePollAsync(PollDto poll);
    public Task<PollDto?> DeletePollAsync(ulong pollId);
}