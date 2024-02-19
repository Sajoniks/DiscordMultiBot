using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using DiscordMultiBot.PollService.Data.Entity;
using DiscordMultiBot.PollService.Repository;

namespace DiscordMultiBot.App.Commands;

public record GetCurrentPollQuery(ulong ChannelId) : IQuery<PollDto>;
public record GetCurrentPollMetadata(ulong ChannelId) : IQuery<PollMetadataDto>;
public record GetCurrentPollResults(ulong ChannelId) : IQuery<IEnumerable<PollVoteResultDto>>;
public record GetNumVotes(ulong ChannelId) : IQuery<int>;
public record GetPollOptionsTemplate(ulong GuildId, string Name) : IQuery<PollOptions>;


public sealed class GetCurrentPollQueryHandler : IQueryHandler<GetCurrentPollQuery, PollDto>
{
    private readonly IPollRepository _pollRepository;
    
    public GetCurrentPollQueryHandler(IPollRepository pollRepository)
    {
        _pollRepository = pollRepository;
    }
    
    public async Task<ResultDto<PollDto>> AskAsync(GetCurrentPollQuery query)
    {
        try
        {
            var p = await _pollRepository.GetPollAsync(query.ChannelId);
            return ResultDto.CreateOK(p);
        }
        catch (DoesNotExistException)
        {
            return ResultDto.CreateError<PollDto>("Poll does not exist");
        }
    }
}

public sealed class GetPollOptionsTemplateHandler : IQueryHandler<GetPollOptionsTemplate, PollOptions>
{
    private readonly IPollRepository _repository;

    public GetPollOptionsTemplateHandler(IPollRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<ResultDto<PollOptions>> AskAsync(GetPollOptionsTemplate query)
    {
        try
        {
            var opts = await _repository.GetPollTemplateAsync(query.GuildId, query.Name);
            return ResultDto.CreateOK(opts);
        }
        catch (Exception)
        {
            return ResultDto.CreateError<PollOptions>("Failed to get template");
        }
    }
}

public sealed class GetCurrentPollMetadataQueryHandler : IQueryHandler<GetCurrentPollMetadata, PollMetadataDto>
{
    private readonly IPollRepository _repository;
    
    public GetCurrentPollMetadataQueryHandler(IPollRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<ResultDto<PollMetadataDto>> AskAsync(GetCurrentPollMetadata query)
    {
        try
        {
            var pm = await _repository.GetPollMetadataAsync(query.ChannelId);
            return ResultDto.CreateOK(pm);
        }
        catch (DoesNotExistException)
        {
            return ResultDto.CreateError<PollMetadataDto>("Metadata or poll does not exist");
        }
    }
}

public sealed class GetCurrentPollResultsQueryHandler : IQueryHandler<GetCurrentPollResults, IEnumerable<PollVoteResultDto>>
{
    private readonly IPollRepository _repository;

    public GetCurrentPollResultsQueryHandler(IPollRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultDto<IEnumerable<PollVoteResultDto>>> AskAsync(GetCurrentPollResults query)
    {
        return ResultDto.CreateOK(await _repository.GetPollResultsByChannelAsync(query.ChannelId));
    }
}

public sealed class GetNumVotesQueryHandler : IQueryHandler<GetNumVotes, int>
{
    private readonly IPollRepository _repository;

    public GetNumVotesQueryHandler(IPollRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultDto<int>> AskAsync(GetNumVotes query)
    {
        return ResultDto.CreateOK(await _repository.GetNumReadyAsync(query.ChannelId));
    }
}