using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;

[Table("PollMetadata")]
public class PollMetadata
{
    [Column("PollID"), NotNull]
    public ulong PollId { get; set; }
    
    [Column("MessageID"), NotNull]
    public ulong MessageId { get; set; }
    
    [Column("ChannelID"), NotNull]
    public ulong ChannelId { get; set; }
}