﻿using DiscordMultiBot.PollService.Data.Connection;
using DiscordMultiBot.PollService.Data.Dto;
using LinqToDB;
using LinqToDB.Data;
using Newtonsoft.Json;

namespace DiscordMultiBot.PollService.Repository;

public class PollRepository : IPollRepository
{
    private readonly DbPoll _db;
    
    public PollRepository(DbPoll connection)
    {
        _db = connection;
    }

    private record UserVoteRecord(string Option, string Data);

    public async Task RecordUserVotesToHistoryAsync(ulong channelId, ulong userId)
    {
        // @todo Refactoring of this
        
        var p = await  _db.Polls.FirstOrDefaultAsync(x => x.ChannelId == channelId);
        if (p is null)
        {
            throw new DoesNotExistException();
        }

        List<UserVoteRecord> votes = new();
        using (var reader = _db.ExecuteReader("SELECT VoteOption, Data FROM PollVotes WHERE PollID=$1 AND UserID=$2",
                   new DataParameter("$1", p.Id, DataType.UInt64),
                   new DataParameter("$2", userId, DataType.UInt64)
               ))
        {
            var resultSet = reader.Reader;
            if (resultSet is null)
            {
                throw new DoesNotExistException();
            }
            
            while (await resultSet.ReadAsync())
            {
                votes.Add(new UserVoteRecord(
                    Option: resultSet.GetString(0),
                    Data:   resultSet.GetString(1)
                ));
            }
        }

        using (var tr = await _db.BeginTransactionAsync())
        {
            await _db.ExecuteAsync("DELETE FROM VotesHistory WHERE UserID=$1",
                new DataParameter("$1", userId, DataType.Int64));
            await _db.ExecuteAsync("INSERT INTO VotesHistory (PollType, VoteData, UserID) VALUES ($1, $2, $3)",
                new DataParameter("$1", p.PollType, DataType.Int32),
                new DataParameter("$2", JsonConvert.SerializeObject(votes), DataType.Text),
                new DataParameter("$3", userId, DataType.UInt64)
            );

            await tr.CommitAsync();
        }
    }

    public async Task<IEnumerable<PollVoteDto>> CreateUserVotesFromHistoryInPollAsync(ulong channelId, ulong userId)
    {
        var p = await _db.Polls.FirstOrDefaultAsync(x => x.ChannelId == channelId);
        if (p is null)
        {
            throw new DoesNotExistException();
        }

        string votingData;
        List<PollVoteDto> votesDtos = new();

        using (var reader = _db.ExecuteReader("SELECT PollType, VoteData FROM VotesHistory WHERE UserID=$1",
                   new DataParameter("$1", userId)
               ))
        {
            var resultSet = reader.Reader;
            if (resultSet is null || !await resultSet.ReadAsync())
            {
                return votesDtos;
            }
            
            var pollType = resultSet.GetInt32(0);
            if (pollType != p.PollType)
            {
                return votesDtos;
            }
            
            votingData = resultSet.GetString(1);
        }

        var votes = JsonConvert.DeserializeObject<List<UserVoteRecord>>(votingData);
        if (votes is null)
        {
            return votesDtos;
        }
        
        using (var tr = await _db.BeginTransactionAsync())
        {
            foreach (var voteRecord in votes)
            {
                var pv = await _db.Votes
                    .Where(x => x.Option.Equals(voteRecord.Option) && x.PollId == p.Id && x.UserId == userId)
                    .FirstOrDefaultAsync();

                if (pv is not null)
                {
                    pv.VoteData = voteRecord.Data;
                    await _db.UpdateAsync(pv);
                }
                else
                {
                    pv = await _db.Votes
                        .Value(x => x.Option, voteRecord.Option)
                        .Value(x => x.PollId, p.Id)
                        .Value(x => x.UserId, userId)
                        .Value(x => x.VoteData, voteRecord.Data)
                        .InsertWithOutputAsync();
                }

                votesDtos.Add(new PollVoteDto(
                    Id: pv.Id,
                    UserId: pv.UserId,
                    PollId: pv.PollId,
                    VoteOption: pv.Option,
                    VoteData: pv.VoteData)
                );
            }

            await tr.CommitAsync();
        }

        return votesDtos;
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
            Options: PollOptions.FromString(added.Options),
            NumMembers: added.NumMembers,
            IsAnonymous: added.IsAnonymous
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
            Options: PollOptions.FromString(p.Options),
            NumMembers: p.NumMembers,
            IsAnonymous: p.IsAnonymous
        );
    }

    ///<inheritdoc/>
    public async Task<PollMetadataDto> AddOrUpdatePollMetadataAsync(PollMetadataDto mDto)
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
            metadata = await _db.PollMetadata
                .Value(x => x.PollId, p.Id)
                .Value(x => x.ChannelId, mDto.ChannelId)
                .Value(x => x.MessageId, mDto.MessageId)
                .InsertWithOutputAsync();
        }
        else
        {
            metadata.ChannelId = mDto.ChannelId;
            metadata.MessageId = mDto.MessageId;
            await _db.UpdateAsync(metadata);
        }

        return new PollMetadataDto(
            ChannelId: metadata.ChannelId,
            MessageId: metadata.MessageId
        );
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
                Options: PollOptions.FromString(r.Options),
                NumMembers: r.NumMembers,
                IsAnonymous: r.IsAnonymous
            );
        }
        else
        {
            await tr.RollbackAsync();
        }

        throw new DoesNotExistException();
    }

    /// <inheridoc />
    public async Task CreatePollTemplateAsync(ulong guildId, string name, PollOptions options)
    {
        if (options.Count == 0 || name.Length == 0)
        {
            throw new ArgumentException();
        }

        await DeletePollTemplateAsync(guildId, name);
        await _db.ExecuteAsync("INSERT INTO PollPreset (GuildId, Name, Preset) VALUES($1, $2, $3)",
            new DataParameter("$1", guildId, DataType.Int64),
            new DataParameter("$2", name, DataType.Text),   
            new DataParameter("$3", options.ToString(), DataType.Text)
        );
    }

    /// <inheridoc />
    public async Task DeletePollTemplateAsync(ulong guildId, string name)
    {
        if (name.Length == 0)
        {
            throw new ArgumentException();
        }
        
        await _db.ExecuteAsync("DELETE FROM PollPreset WHERE Name=$1 AND GuildId=$2", 
            new DataParameter("$1", name, DataType.Text),
            new DataParameter("$2", guildId, DataType.Int64)
        );
    }

    public async Task<PollOptions> GetPollTemplateAsync(ulong guildId, string name)
    {
        if (name.Length == 0)
        {
            throw new ArgumentException();
        }

        var preset = await _db.ExecuteAsync<string>("SELECT Preset FROM PollPreset WHERE GuildId=$1 AND Name=$2",
            new DataParameter("$1", guildId),
            new DataParameter("$2", name)
        );
        
        return PollOptions.FromString(preset);
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

    public async Task<int> GetNumReadyAsync(ulong channelId)
    {
        var p = await _db.Polls
            .Where(x => x.ChannelId == channelId)
            .FirstOrDefaultAsync();
        
        if (p is null)
        {
            throw new DoesNotExistException();
        }

        var r = await _db.ExecuteAsync<int>("SELECT COUNT(*) FROM VoterStates WHERE State=$1 AND PollID=$2",
            new DataParameter("$1", (int)PollVoterState.Ready, DataType.Int32), 
            new DataParameter("$2", p.Id, DataType.UInt32)
        );
        return r;
    }

    ///<inheritdoc/>
    public async Task<PollVoterStateDto> UpdateUserPollVoteStateAsync(ulong channelId, ulong userId)
    {
        var p = await _db.Polls
            .Where(x => x.ChannelId == channelId)
            .FirstOrDefaultAsync();
        
        if (p is null)
        {
            throw new DoesNotExistException();
        }
        
        var r = await _db.VoterStates
            .Where(x => x.PollId == p.Id && x.UserId == userId)
            .FirstOrDefaultAsync();

        if (r is not null)
        {
            r.State = (r.State + 1) % 2;
            await _db.UpdateAsync(r);
        }
        else
        {
            r = await _db.VoterStates
                .Value(x => x.State, (int)PollVoterState.Ready)
                .Value(x => x.UserId, userId)
                .Value(x => x.PollId, p.Id)
                .InsertWithOutputAsync();
        }
        
        return new PollVoterStateDto(
            Id: r.Id,
            UserId: r.UserId,
            PollId: r.PollId,
            VoterState: (PollVoterState)r.State
        );
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