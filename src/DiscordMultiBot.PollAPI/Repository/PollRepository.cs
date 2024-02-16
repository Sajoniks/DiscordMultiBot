using DiscordMultiBot.PollService.Data.Connection;
using DiscordMultiBot.PollService.Data.Dto;
using LinqToDB;

namespace DiscordMultiBot.PollService.Repository;

public class PollRepository : IPollRepository
{
    private readonly DbPoll _db;
    
    public PollRepository(DbPoll connection)
    {
        _db = connection;
    }
    
    ///<inheritdoc/>
    public async Task<PollDto> CreatePollAsync(ulong channelId, PollType type, PollOptions options, bool isAnon,
        int numMembers)
    {
        var p = _db.Polls.FirstOrDefault(x => x.ChannelId == channelId);
        if (p is not null)
        {
            throw new AlreadyExistsException();
        }
        
        var added = await _db.Polls
                .Value(x => x.Options, options.ToString())
                .Value(x => x.ChannelId, channelId)
                .Value(x => x.PollType, (int) type)
                .Value(x => x.IsAnonymous, isAnon)
                .Value(x => x.NumMembers, numMembers)
                .InsertWithOutputAsync();
        
        return new PollDto(
            Id: added.Id,
            ChannelId: added.ChannelId,
            Type: (PollType) added.PollType,
            Options: PollOptions.FromString(added.Options)
        );
    }

    public async Task<PollMetadataDto> GetPollMetadataAsync(ulong channelId)
    {
        var p = await _db.Polls
            .Where(x => x.ChannelId == channelId)
            .FirstOrDefaultAsync();

        if (p is null)
        {
            throw new DoesNotExistException();
        }
        
        var metadata = await _db.PollMetadata
            .Where(x => x.PollId == p.Id)
            .FirstOrDefaultAsync();

        if (metadata is null)
        {
            throw new DoesNotExistException();
        }

        return new PollMetadataDto(
            ChannelId: metadata.ChannelId,
            MessageId: metadata.MessageId
        );
    }

    ///<inheridoc/>
    public async Task<PollDto> GetPollAsync(ulong channelId)
    {
        var p = await _db.Polls
            .Where(x => x.ChannelId == channelId)
            .FirstOrDefaultAsync();

        if (p is null)
        {
            throw new DoesNotExistException();
        }

        return new PollDto(
            Id: p.Id,
            ChannelId: p.ChannelId,
            Type: (PollType)p.PollType,
            Options: PollOptions.FromString(p.Options)
        );
    }

    ///<inheritdoc/>
    public async Task AddOrUpdatePollMetadataAsync(PollMetadataDto mDto)
    {
        var p = await _db.Polls
            .Where(x => x.ChannelId == mDto.ChannelId)
            .FirstOrDefaultAsync();

        if (p is null)
        {
            throw new DoesNotExistException();
        }

        var metadata = await _db.PollMetadata
            .Where(x => x.PollId == p.Id)
            .FirstOrDefaultAsync();

        if (metadata is null)
        {
            await _db.PollMetadata
                .Value(x => x.PollId, p.Id)
                .Value(x => x.ChannelId, mDto.ChannelId)
                .Value(x => x.MessageId, mDto.MessageId)
                .InsertAsync();
        }
        else
        {
            metadata.ChannelId = mDto.ChannelId;
            metadata.MessageId = mDto.MessageId;
            await _db.UpdateAsync(metadata);
        }
    }

    ///<inheritdoc/>
    public async Task<PollDto> DeletePollByChannelAsync(ulong channelId)
    {
        using var tr = await _db.BeginTransactionAsync();
        var r = await _db.Polls
            .Where(x => x.ChannelId == channelId)
            .FirstOrDefaultAsync();
        
        if (r is not null)
        {
            await _db.Polls
                .Where(x => x.Id == r.Id)
                .DeleteAsync();
            
            await _db.Votes
                .Where(x => x.PollId == r.Id)
                .DeleteAsync();

            await _db.VoterStates
                .Where(x => x.PollId == r.Id)
                .DeleteAsync();
            
            var ms = await _db.PollMetadata
                .Where(x => x.PollId == r.Id)
                .DeleteWithOutputAsync();
            
            var m = ms
                .Select(x => new PollMetadataDto(ChannelId: x.ChannelId, MessageId: x.MessageId))
                .FirstOrDefault();

            await tr.CommitAsync();
            
            return new PollDto(
                Id: r.Id,
                ChannelId: r.ChannelId,
                Type: (PollType) r.PollType,
                Metadata: m,
                Options: PollOptions.FromString(r.Options)
            );
        }
        else
        {
            await tr.RollbackAsync();
        }

        throw new DoesNotExistException();
    }
    
    ///<inheritdoc/>
    public async Task<PollVoteDto> CreateUserVoteInPollAsync(ulong channelId, ulong userId, string voteOption, string voteData)
    {
        if (voteOption.Length == 0)
        {
            throw new OptionIsInvalidException();
        }
        
        using var tr = await _db.BeginTransactionAsync();

        var p = await _db.Polls
            .Where(x => x.ChannelId == channelId)
            .FirstOrDefaultAsync();

        if (p is null)
        {
            throw new DoesNotExistException();
        }

        if (!p.Options.Contains(voteOption))
        {
            throw new OptionIsInvalidException();
        }

        var r = await _db.Votes
            .Where(x => x.UserId == userId && x.PollId == p.Id && x.Option.Equals(voteOption))
            .FirstOrDefaultAsync();

        if (r is null)
        {
            r = await _db.Votes
                .Value(x => x.PollId, p.Id)
                .Value(x => x.UserId, userId)
                .Value(x => x.VoteData, voteData)
                .Value(x => x.Option, voteOption)
                .InsertWithOutputAsync();

            await _db.VoterStates
                .Value(x => x.State, (int) PollVoterState.NotReady)
                .Value(x => x.UserId, userId)
                .Value(x => x.PollId, p.Id)
                .InsertAsync();
        }
        else
        {
            r.VoteData = voteData;
            await _db.UpdateAsync(r);
        }

        await tr.CommitAsync();
        return new PollVoteDto(
            Id: r.Id,
            UserId: r.UserId,
            PollId: r.PollId,
            VoteData: r.VoteData,
            VoteOption: r.Option
        );
    }

    ///<inheritdoc/>
    public async Task<PollVoterStateDto> UpdateUserPollVoteStateAsync(ulong channelId, ulong userId,
        PollVoterState state)
    {
        var p = await _db.Polls
            .Where(x => x.ChannelId == channelId)
            .FirstOrDefaultAsync();
        
        if (p is null)
        {
            throw new DoesNotExistException();
        }
        
        var r = await _db.VoterStates
            .Where(x => x.PollId == channelId && x.UserId == userId)
            .FirstOrDefaultAsync();

        if (r is not null)
        {
            r.State = (int)state;
            await _db.UpdateAsync(r);

            return new PollVoterStateDto(
                Id: r.Id,
                UserId: r.UserId,
                PollId: r.PollId,
                VoterState: (PollVoterState)r.State
            );
        }
        else
        {
            throw new DoesNotExistException();
        }
    }

    ///<inheritdoc/>
    public Task<IEnumerable<PollVoteResultDto>> GetPollResultsByChannelAsync(ulong channelId)
    {
        var ls = _db.PollResults
            .Where(x => x.ChannelId == channelId)
            .ToList()
            .Select(pr => new PollVoteResultDto(
                PollId: pr.PollId,
                ChannelId: pr.ChannelId,
                UserId: pr.UserId,
                VoteData: pr.VoteData,
                VoteOption: pr.VoteOption,
                VoterState: (PollVoterState)pr.VoterState)
            );

        return Task.FromResult(ls);
    }
}