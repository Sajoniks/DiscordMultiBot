using System.Text;
using Discord;
using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using Newtonsoft.Json;

namespace DiscordMultiBot.App.Modules.Poll;

[Group("poll", "Poll commands")]
public partial class PollModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CommandDispatcher _dispatcher;
    
    public PollModule(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }
    
    [SlashCommand("create", "Start a poll in a current channel")]
    public async Task PollAsync(
        [Summary("options", "List of vote parameters separated with \"++\"")] string optionsString,
        [Summary("participants", "Number of participants in the poll")][MinValue(0)] int participants,
        [Summary("anonymous", "If true, number of votes will be shown")] bool isAnonymous,
        [Choice("YesNo", nameof(PollType.Binary)), Choice("Preference", nameof(PollType.Numeric))] string style
    )
    {
        
        
        var options = PollOptions.FromString(optionsString);
        if (options.Count == 0)
        {
            var e = EmbedXmlUtils.CreateErrorEmbed("Create poll failed", "Options empty");
            await RespondAsync(text: e.Text, embeds: e.Embeds, components: e.Comps, ephemeral: true);
            return;
        }
        else if (options.Count == 1)
        {
            // Finish the poll
            return;
        }
        
        var command =
            new CreatePollCommand(
                ChannelId: Context.Channel.Id, 
                Style: style, 
                NumMembers: participants, 
                PollOptions: options, 
                IsAnonymous: isAnonymous
            );

        var r = await _dispatcher.ExecuteAsync<CreatePollCommand, PollDto>(command);
        if (r.IsOK)
        {
            var creator = new EmbedXmlCreator();
            string layout;
            if (style.Equals(nameof(PollType.Binary)))
            {
                layout = "PollBinary";
            }
            else if (style.Equals(nameof(PollType.Numeric)))
            {
                layout = "PollNumeric";
            }
            else
            {
                EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Create poll failed", "Unknown poll type");
                await RespondAsync(text: e.Text, embeds: e.Embeds, components: e.Comps, ephemeral: true);
                return;
            }

            await RespondAsync(text: "Creating poll...");
            
            EmbedXmlDoc embed = creator.Create(layout);
            var m = await Context.Channel.SendMessageAsync(text: embed.Text, components: embed.Comps, embeds: embed.Embeds);
            await _dispatcher.ExecuteAsync(new UpdatePollMetadataCommand(Context.Channel.Id, m.Id));
        }
        else
        {
            EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Create poll failed", r.Error);
            await RespondAsync(text: e.Text, components: e.Comps, embeds: e.Embeds, ephemeral: true);
        }
    }

    [SlashCommand("clear", "Clear all polls in a current channel, without completion")]
    public async Task ClearPollAsync()
    {
        var command = new DeletePollCommand(Context.Channel.Id);

        var r = await _dispatcher.ExecuteAsync<DeletePollCommand, PollDto>(command);
        EmbedXmlDoc e;
        if (r.IsOK)
        {
            e = EmbedXmlUtils.CreateResponseEmbed("Deleted poll", "Polls in channel were deleted"); 
            if (r.Result?.Metadata is not null)
            {
               _ = Context.Channel
                    .GetMessageAsync(r.Result.Metadata.MessageId)
                    .ContinueWith(ms => ms.Result?.DeleteAsync());
            }
        }
        else
        {
            e = EmbedXmlUtils.CreateErrorEmbed("Failed to delete polls", r.Error);
        }
        await RespondAsync(text: e.Text, components: e.Comps, embeds: e.Embeds, ephemeral: true);
    }

    [SlashCommand("complete", "Complete a poll in a current channel")]
    public async Task CompletePollAsync()
    {
        var getResultsQuery = new GetCurrentPollResults(Context.Channel.Id);
        var getResults = await _dispatcher.QueryAsync<GetCurrentPollResults, IEnumerable<PollVoteResultDto>>(getResultsQuery);
        
        var deletePollCommand = new DeletePollCommand(Context.Channel.Id);
        var p = await _dispatcher.ExecuteAsync<DeletePollCommand, PollDto>(deletePollCommand);

        if (p.IsOK && getResults.IsOK)
        {
            PollDto poll = p.Result!;

            _ = Context.Channel.DeleteMessageAsync(poll.Metadata!.MessageId);
            
            await RespondAsync("Computing...");
            
            switch (p.Result!.Type)
            {
                case PollType.Binary:
                    await RespondBinaryPollCompletedAsync(poll, getResults.Result!);
                    break;
                
                case PollType.Numeric:
                    await RespondHandleNumericPollCompletedAsync(poll, getResults.Result!);
                    break;
            }
        }
    }
}