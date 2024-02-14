using LinqToDB.Mapping;

namespace DiscordMultiBot.PollService.Data.Entity;

[System.ComponentModel.DataAnnotations.Schema.Table("ChannelMapping")]
public class ChannelMapping
{
    [PrimaryKey, Identity, Column("ID")]
    public ulong Id { get; set; }
 
    [Column("GuildID"), NotNull]
    public ulong GuildId { get; set; }
    
    [Column("MappingData"), NotNull]
    public string MappingData { get; set; } = "";
}