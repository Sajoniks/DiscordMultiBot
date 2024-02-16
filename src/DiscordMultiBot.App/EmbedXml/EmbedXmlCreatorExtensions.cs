using Discord;

namespace DiscordMultiBot.App.EmbedXml;

public static class EmbedXmlUtils
{
    public static EmbedXmlDoc CreateErrorEmbed(string title, string desc)
    {
        var creator = new EmbedXmlCreator();
        creator.Bindings.Add("Title", title);
        creator.Bindings.Add("Desc", desc);
        return creator.Create("Error");
    }
    
    public static EmbedXmlDoc CreateResponseEmbed(string title, string desc)
    {
        var creator = new EmbedXmlCreator();
        creator.Bindings.Add("Title", title);
        creator.Bindings.Add("Desc", desc);
        return creator.Create("Response");
    }
}