using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;

public enum PollType
{
    Numeric,
    Binary
}

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
    public PollType PollType { get; set; }
}