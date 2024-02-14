using DiscordMultiBot.PollService.Data.Connection;
using DiscordMultiBot.PollService.Data.Dto;
using DiscordMultiBot.PollService.Data.Entity;
using LinqToDB;

namespace DiscordMultiBot.PollService.Repository;

public class PollRepository : IPollRepository
{
    private readonly DbPoll _db;
    
    public PollRepository(DbPoll connection)
    {
        _db = connection;
    }
    
    public async Task<PollDto?> CreatePollAsync(PollDto poll)
    {
        var p = _db.Polls.FirstOrDefault(x => x.Id == poll.Id);
        if (p is not null)
        {
            return new PollDto(
                Id: p.Id, 
                ChannelId: p.ChannelId, 
                Options: PollOptions.FromString(p.Options)
            );
        }
        
        var added = await _db.Polls
                .Value(x => x.Options, poll.Options.ToString())
                .Value(x => x.ChannelId, poll.ChannelId)
                .InsertWithOutputAsync();

        return new PollDto(
            Id: added.Id,
            ChannelId: added.ChannelId,
            Options: PollOptions.FromString(added.Options)
        );
    }

    public async Task<PollDto?> DeletePollAsync(ulong pollId)
    {
        var rs = await _db.Polls
            .Where(x => x.Id == pollId)
            .DeleteWithOutputAsync();
        var r = rs.FirstOrDefault();

        if (r is not null)
        {
            return new PollDto(
                Id: r.Id,
                ChannelId: r.ChannelId,
                Options: PollOptions.FromString(r.Options)
            );
        }

        return null;
    }
}