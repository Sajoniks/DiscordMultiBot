using DiscordMultiBot.PollService.Data;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Repository;


public interface IVoteRepository : IRepository<PollVoteDto>
{
    public Task<PollVoteDto?> CreateVote(PollVoteDto vote);
    public Task<PollVoteDto?> DeleteVote(ulong voteId);
}