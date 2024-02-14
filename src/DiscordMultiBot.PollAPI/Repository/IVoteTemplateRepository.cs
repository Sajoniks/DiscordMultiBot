using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Data.Template;


public interface IVoteTemplateRepository : IRepository<PollVoteTemplateDto>
{
    public Task<PollVoteTemplateDto?> CreateTemplateAsync();
    public Task<PollVoteTemplateDto?> DeleteTemplateAsync();
}