using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;

public enum VoterStateValue
{
    NotReady,
    Ready
}

[Table("VoterStates")]
public class VoterState
{
    [PrimaryKey, Column("ID"), Identity]
    public ulong Id { get; set; }
    
    [Column("UserID"), NotNull]
    public ulong UserId { get; set; }
    
    [Column("State"), NotNull]
    public VoterStateValue State { get; set; } = VoterStateValue.NotReady;
    
    [Column("PollID"), NotNull]
    public ulong PollId { get; set; }
    
    [Association(ThisKey = nameof(PollId), OtherKey = nameof(Poll.Id))]
    public Poll Poll { get; set; }
}