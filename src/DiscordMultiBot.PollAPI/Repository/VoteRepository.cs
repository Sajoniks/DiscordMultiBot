using DiscordMultiBot.PollService.Data.Connection;
using DiscordMultiBot.PollService.Data.Dto;
using DiscordMultiBot.PollService.Data.Entity;
using LinqToDB;

namespace DiscordMultiBot.PollService.Repository;

public class VoteRepository : IVoteRepository
{
    private readonly DbPoll _db;
    
    public VoteRepository(DbPoll connection)
    {
        _db = connection;
    }
    
    public async Task<PollVoteDto?> CreateVote(PollVoteDto vote)
    {
        var v = _db.Votes.FirstOrDefault(x => x.Id == vote.Id);
        if (v is null)
        {
            await _db.Votes
                .Value(x => x.PollId, vote.PollId)
                .Value(x => x.VoteData, vote.VoteData)
                .InsertAsync();
        }
        else
        {
            v.VoteData = vote.VoteData;
            await _db.UpdateAsync(v);
        }

        return new PollVoteDto(
            Id: vote.Id, 
            UserId: vote.UserId,
            PollId: vote.PollId,
            VoteData: vote.VoteData
        );
    }

    public async Task<PollVoteDto?> DeleteVote(ulong voteId)
    {
        var rs = await _db.Votes
            .Where(x => x.Id == voteId)
            .DeleteWithOutputAsync();

        var r = rs.FirstOrDefault();
        if (r is not null)
        {
            return new PollVoteDto(
                Id: r.Id,
                UserId: r.UserId,
                PollId: r.PollId,
                VoteData: r.VoteData
            );
        }

        return null;
    }
}