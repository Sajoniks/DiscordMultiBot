using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using DiscordMultiBot.PollService.Repository;

namespace DiscordMultiBot.App.Commands;

public record CreatePollCommand(ulong ChannelId, string Style, int NumMembers, PollOptions PollOptions, bool IsAnonymous) : ICommand<PollDto>;
public record DeletePollCommand(ulong ChannelId) : ICommand<PollDto>;
public record UpdatePollVoterStateCommand(ulong ChannelId, ulong UserId) : ICommand<PollVoterStateDto>;
public record UpdatePollMetadataCommand(ulong ChannelId, ulong MessageId) : ICommand<PollMetadataDto>;
public record CreatePollVoteCommand(ulong ChannelId, ulong UserId, string VoteOption, string VoteData) : ICommand;
public record CreatePollVotesFromHistoryCommand(ulong ChannelId, ulong UserId) : ICommand<IEnumerable<PollVoteDto>>;
public record WriteCurrentUserVotesToHistoryCommand(ulong ChannelId, ulong UserId) : ICommand;
public record CreatePollOptionsTemplateCommand(ulong GuildId, string Name, PollOptions Options) : ICommand;
public record DeletePollOptionsTemplateCommand(ulong GuildId, string Name) : ICommand;

public sealed class CreatePollCommandHandler : ICommandHandler<CreatePollCommand, PollDto>
{
    private readonly IPollRepository _pollRepository;
    
    public CreatePollCommandHandler(IPollRepository pollRepository)
    {
        _pollRepository = pollRepository;
    }
    
    public async Task<ResultDto<PollDto>> ExecuteAsync(CreatePollCommand command)
    {
        if (!command.PollOptions.Any())
        {
            return ResultDto.CreateError<PollDto>("Empty options");
        }

        try
        {
            var p = await _pollRepository.CreatePollAsync(
                channelId: command.ChannelId,
                type: Enum.Parse<PollType>(command.Style),
                options: command.PollOptions,
                isAnon: command.IsAnonymous,
                numMembers: command.NumMembers
            );
            return ResultDto.CreateOK(p);
        }
        catch (AlreadyExistsException)
        {
            return ResultDto.CreateError<PollDto>("Poll already exists");
        }
    }
}

public sealed class DeletePollOptionsTemplateCommandHandler : ICommandHandler<DeletePollOptionsTemplateCommand>
{
    private IPollRepository _pollRepository;

    public DeletePollOptionsTemplateCommandHandler(IPollRepository pollRepository)
    {
        _pollRepository = pollRepository;
    }

    public async Task<ResultDto> ExecuteAsync(DeletePollOptionsTemplateCommand command)
    {
        try
        {
            await _pollRepository.DeletePollTemplateAsync(command.GuildId, command.Name);
            return ResultDto.CreateOK();
        }
        catch (Exception)
        {
            return ResultDto.CreateError("Failed to delete template");
        }
    }
}

public sealed class DeletePollCommandHandler : ICommandHandler<DeletePollCommand, PollDto>
{
    private readonly IPollRepository _pollRepository;
    
    public DeletePollCommandHandler(IPollRepository pollRepository)
    {
        _pollRepository = pollRepository;
    }
    
    public async Task<ResultDto<PollDto>> ExecuteAsync(DeletePollCommand command)
    {
        try
        {
            var p = await _pollRepository.DeletePollByChannelAsync(command.ChannelId);
            return ResultDto.CreateOK(p);
        }
        catch (DoesNotExistException)
        {
            return ResultDto.CreateError<PollDto>("Poll does not exist");
        }
    }
}

public sealed class UpdatePollVoterStateCommandHandler : ICommandHandler<UpdatePollVoterStateCommand, PollVoterStateDto>
{
    private readonly IPollRepository _pollRepository;
    
    public UpdatePollVoterStateCommandHandler(IPollRepository repository)
    {
        _pollRepository = repository;
    }
    
    public async Task<ResultDto<PollVoterStateDto>> ExecuteAsync(UpdatePollVoterStateCommand command)
    {
        try
        {
            var r = await _pollRepository.UpdateUserPollVoteStateAsync(command.ChannelId, command.UserId);
            return ResultDto.CreateOK(r);
        }
        catch (DoesNotExistException)
        {
            return ResultDto.CreateError<PollVoterStateDto>("Poll does not exist");
        }
    }
}

public sealed class UpdatePollMetadataCommandHandler : ICommandHandler<UpdatePollMetadataCommand, PollMetadataDto>
{
    private readonly IPollRepository _pollRepository;
    
    public UpdatePollMetadataCommandHandler(IPollRepository pollRepository)
    {
        _pollRepository = pollRepository;
    }
    
    public async Task<ResultDto<PollMetadataDto>> ExecuteAsync(UpdatePollMetadataCommand command)
    {
        try
        {
            var r = await _pollRepository.AddOrUpdatePollMetadataAsync(
                new PollMetadataDto(command.ChannelId, command.MessageId));
            return ResultDto.CreateOK(r);
        }
        catch (DoesNotExistException)
        {
            return ResultDto.CreateError<PollMetadataDto>("Poll does not exist");
        }
    }
}

public sealed class CreatePollOptionsTemplateCommandHandler : ICommandHandler<CreatePollOptionsTemplateCommand>
{
    private readonly IPollRepository _pollRepository;
    public CreatePollOptionsTemplateCommandHandler(IPollRepository pollRepository)
    {
        _pollRepository = pollRepository;
    }

    public async Task<ResultDto> ExecuteAsync(CreatePollOptionsTemplateCommand command)
    {
        try
        {
            await _pollRepository.CreatePollTemplateAsync(command.GuildId, command.Name, command.Options);
            return ResultDto.CreateOK();
        }
        catch (Exception)
        {
            return ResultDto.CreateError("Failed to create template"); 
        }
    }
}

public sealed class WriteCurrentUserVotesToHistoryCommandHandler : ICommandHandler<WriteCurrentUserVotesToHistoryCommand>
{
    private readonly IPollRepository _repository;

    public WriteCurrentUserVotesToHistoryCommandHandler(IPollRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<ResultDto> ExecuteAsync(WriteCurrentUserVotesToHistoryCommand command)
    {
        try
        {
            await _repository.RecordUserVotesToHistoryAsync(command.ChannelId, command.UserId);
            return ResultDto.CreateOK();
        }
        catch (Exception)
        {
            return ResultDto.CreateError("Failed to record votes");
        }
    }
}

public sealed class CreateVoteCommandHandler : ICommandHandler<CreatePollVoteCommand>
{
    private readonly IPollRepository _pollPollRepository;

    public CreateVoteCommandHandler(IPollRepository pollRepository)
    {
        _pollPollRepository = pollRepository;
    }
    
    public async Task<ResultDto> ExecuteAsync(CreatePollVoteCommand command)
    {
        try
        {
            await _pollPollRepository.CreateUserVoteInPollAsync(
                channelId: command.ChannelId, 
                userId: command.UserId, 
                voteOption: command.VoteOption, 
                voteData: command.VoteData
            );
            
            return ResultDto.CreateOK();
        }
        catch (DoesNotExistException)
        {
            return ResultDto.CreateError("Poll does not exist");
        }
    }
}

public sealed class CreatePollVotesFromHistoryCommandHandler : ICommandHandler<CreatePollVotesFromHistoryCommand, IEnumerable<PollVoteDto>>
{
    private readonly IPollRepository _repository;

    public CreatePollVotesFromHistoryCommandHandler(IPollRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<ResultDto<IEnumerable<PollVoteDto>>> ExecuteAsync(CreatePollVotesFromHistoryCommand command)
    {
        try
        {
            var r= await _repository.CreateUserVotesFromHistoryInPollAsync(command.ChannelId, command.UserId);
            return ResultDto.CreateOK(r);
        }
        catch (Exception)
        {
            return ResultDto.CreateError<IEnumerable<PollVoteDto>>("Failed to make votes");
        }
    }
}