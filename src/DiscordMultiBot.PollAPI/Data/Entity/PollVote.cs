using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;

[Table("PollVotes")]
public class PollVote
{
    [PrimaryKey, Column("ID"), Identity]
    public ulong Id { get; set; }
    
    [Column("PollID"), NotNull]
    public ulong PollId { get; set; }
    
    [Column("UserID"), NotNull]
    public ulong UserId { get; set; }
    
    [Column("Data"), NotNull]
    public string VoteData { get; set; }
    
    [Association(ThisKey = nameof(PollId), OtherKey = nameof(Poll.Id))]
    public Poll Poll { get; set; }
}