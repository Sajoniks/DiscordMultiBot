using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Repository;

public class VoteTemplateRepository : IVoteTemplateRepository
{
    public Task<PollVoteTemplateDto?> CreateTemplateAsync()
    {
        throw new NotImplementedException();
    }

    public Task<PollVoteTemplateDto?> DeleteTemplateAsync()
    {
        throw new NotImplementedException();
    }
}