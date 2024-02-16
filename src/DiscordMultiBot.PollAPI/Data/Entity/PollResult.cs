using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;

[Table("View_PollResult", IsView = true)]
public class PollResult
{
    [Column("PollID")]
    public ulong PollId { get; set; }
    
    [Column("ChannelID")]
    public ulong ChannelId { get; set; }
    
    [Column("UserID")]
    public ulong UserId { get; set; }
    
    [Column("Data")]
    public string VoteData { get; set; } = "";
    
    [Column("VoterState")]
    public int VoterState { get; set; }
    
    [Column("Option")]
    public string VoteOption { get; set; }
}