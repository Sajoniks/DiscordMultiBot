using Discord;

namespace DiscordMultiBot.App.EmbedXml;

public record EmbedXmlDoc(string Text, Embed[] Embeds);

public interface IEmbedXmlCreator
{
    EmbedXmlDoc Create(string layoutName);
}