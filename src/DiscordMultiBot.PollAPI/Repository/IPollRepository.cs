using DiscordMultiBot.PollService.Data;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Repository;


public class AlreadyExistsException : Exception { }
public class DoesNotExistException : Exception { }
public class OptionIsInvalidException : Exception { }

public interface IPollRepository : IRepository
{
    ///<exception cref="AlreadyExistsException">Poll is already created</exception>
    public Task<PollDto> CreatePollAsync(ulong channelId, PollType type, PollOptions options, bool isAnon,
        int numMembers);
    
    ///<exception cref="DoesNotExistException"></exception>
    public Task AddOrUpdatePollMetadataAsync(PollMetadataDto metadata);
    
    ///<exception cref="DoesNotExistException"></exception>
    public Task<PollMetadataDto> GetPollMetadataAsync(ulong channelId);

    ///<exception cref="DoesNotExistException"></exception>
    public Task<PollDto> GetPollAsync(ulong channelId);

    ///<exception cref="DoesNotExistException">Poll does not exist</exception>
    public Task<PollDto> DeletePollByChannelAsync(ulong channelId);
    
    
    ///<exception cref="DoesNotExistException">Poll does not exist</exception>
    public Task<PollVoteDto> CreateUserVoteInPollAsync(ulong channelId, ulong userId, string voteOption, string voteData);
    ///<exception cref="DoesNotExistException">Poll or vote does not exist</exception>
    public Task<PollVoterStateDto> UpdateUserPollVoteStateAsync(ulong channelId, ulong userId, PollVoterState state);
    public Task<IEnumerable<PollVoteResultDto>> GetPollResultsByChannelAsync(ulong channelId);
}