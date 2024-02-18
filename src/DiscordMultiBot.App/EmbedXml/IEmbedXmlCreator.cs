using Discord;

namespace DiscordMultiBot.App.EmbedXml;

public record EmbedXmlDoc(string Text, Embed[] Embeds, MessageComponent Comps);

public interface IEmbedXmlCreator
{
    EmbedXmlDoc Create(string layoutName);
}