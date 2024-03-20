using Discord;

namespace DiscordMultiBot.App.EmbedXml;

public static class EmbedXmlUtils
{
    public static EmbedXmlDoc CreateErrorEmbed(string title, string desc, Action<EmbedXmlCreator>? callback = null)
    {
        var creator = new EmbedXmlCreator();
        callback?.Invoke(creator);
        creator.Bindings.Add("Title", title);
        creator.Bindings.Add("Desc", desc);
        return creator.Create("Error");
    }
    
    public static EmbedXmlDoc CreateResponseEmbed(string title, string desc, Action<EmbedXmlCreator>? callback = null)
    {
        var creator = new EmbedXmlCreator();
        callback?.Invoke(creator);
        creator.Bindings.Add("Title", title);
        creator.Bindings.Add("Desc", desc);
        return creator.Create("Response");
    }

    public static Task<IUserMessage> SendMessageFromXmlAsync(this EmbedXmlDoc doc, IMessageChannel channel)
    {
        return channel.SendMessageAsync(text: doc.Text, embeds: doc.Embeds, components: doc.Comps);
    }

    public static Task ModifyOriginalResponseFromXmlAsync(this EmbedXmlDoc doc, IInteractionContext context)
    {
        return context.Interaction.ModifyOriginalResponseAsync(prps =>
        {
            prps.Embeds = doc.Embeds;
            prps.Content = doc.Text;
            prps.Components = doc.Comps;
        });
    }
    
    public static Task ModifyMessageFromXmlAsync(this EmbedXmlDoc doc, ulong messageId, IMessageChannel channel)
    {
        return channel.ModifyMessageAsync(messageId, (prps) =>
        {
            prps.Embeds = doc.Embeds;
            prps.Content = doc.Text;
            prps.Components = doc.Comps;
        });
    }

    public static Task RespondFromXmlAsync(this EmbedXmlDoc doc, IInteractionContext context, bool ephemeral = false)
    {
        return context.Interaction.RespondAsync(text: doc.Text, embeds: doc.Embeds, components: doc.Comps, ephemeral: ephemeral);
    }
}