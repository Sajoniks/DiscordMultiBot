using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using DiscordMultiBot.PollService.Data.Entity;
using DiscordMultiBot.PollService.Repository;

namespace DiscordMultiBot.App.Commands;

public record CreatePollCommand(ulong ChannelId, string Style, int NumMembers, PollOptions PollOptions, bool IsAnonymous) : ICommand;
public record DeletePollCommand(ulong ChannelId) : ICommand;
public record UpdatePollMetadataCommand(ulong ChannelId, ulong MessageId) : ICommand;
public record UpdatePollVoterStateCommand(ulong ChannelId, ulong UserId) : ICommand;
public record CreatePollVoteCommand(ulong ChannelId, ulong UserId, string VoteOption, string VoteData) : ICommand;

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

public sealed class UpdatePollMetadataCommandHandler : ICommandHandler<UpdatePollMetadataCommand>
{
    private readonly IPollRepository _pollRepository;

    public UpdatePollMetadataCommandHandler(IPollRepository pollRepository)
    {
        _pollRepository = pollRepository;
    }
    
    public async Task<ResultDto> ExecuteAsync(UpdatePollMetadataCommand command)
    {
        try
        {
            await _pollRepository.AddOrUpdatePollMetadataAsync(
                new PollMetadataDto(command.ChannelId, command.MessageId));
            return ResultDto.CreateOK();
        }
        catch (DoesNotExistException)
        {
            return ResultDto.CreateError("Poll does not exist");
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