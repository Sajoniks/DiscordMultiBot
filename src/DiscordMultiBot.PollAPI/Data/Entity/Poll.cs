using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;

[Table("Polls")]
public class Poll
{
    [Column("ID"), PrimaryKey, Identity]
    public ulong Id { get; set; }
    
    [Column("ChannelID"), NotNull]
    public ulong ChannelId { get; set; }
    
    [Column, NotNull]
    public string Options { get; set; } = "";

    [Column, NotNull]
    public bool IsAnonymous { get; set; } = false;

    [Column, NotNull]
    public int NumMembers { get; set; }
    
    [Column, NotNull]
    public int PollType { get; set; }
}