using DiscordMultiBot.PollService.Data.Entity;
using LinqToDB;
using LinqToDB.Data;

namespace DiscordMultiBot.PollService.Data.Connection;

public class DbPoll : DataConnection
{
    public DbPoll(DataOptions dataOptions) : base(dataOptions) { }

    public ITable<Poll> Polls => this.GetTable<Poll>();
    public ITable<PollVote> Votes => this.GetTable<PollVote>();
    public ITable<VoterState> VoterStates => this.GetTable<VoterState>();
}