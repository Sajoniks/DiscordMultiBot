using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;


[Table("VoterStates")]
public class VoterState
{
    [PrimaryKey, Column("ID"), Identity]
    public ulong Id { get; set; }
    
    [Column("UserID"), NotNull]
    public ulong UserId { get; set; }
    
    [Column("State"), NotNull]
    public int State { get; set; }
    
    [Column("PollID"), NotNull]
    public ulong PollId { get; set; }
    
    [Association(ThisKey = nameof(PollId), OtherKey = nameof(Poll.Id))]
    public Poll VotedPoll { get; set; }
}