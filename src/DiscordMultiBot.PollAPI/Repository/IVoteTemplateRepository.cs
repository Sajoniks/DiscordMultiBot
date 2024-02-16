using DiscordMultiBot.PollService.Data;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Repository;


public interface IVoteTemplateRepository : IRepository
{
    public Task<PollVoteTemplateDto?> CreateTemplateAsync();
    public Task<PollVoteTemplateDto?> DeleteTemplateAsync();
}