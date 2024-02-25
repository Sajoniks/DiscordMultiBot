using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.App.Modules.Poll;

[Group("poll", "Poll commands")]
public partial class PollModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CommandDispatcher _dispatcher;
    private readonly BotCommandDispatcher _botDispatcher;
    
    public PollModule(CommandDispatcher dispatcher, BotCommandDispatcher botDispatcher)
    {
        _dispatcher = dispatcher;
        _botDispatcher = botDispatcher;
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
            await EmbedXmlUtils
                .CreateErrorEmbed("Create poll failed", "Options empty")
                .RespondFromXmlAsync(Context, ephemeral: true);
            
            return;
        }

        var r = await _botDispatcher.ExecuteAsync(Context, new CreatePollBotCommand(
            PollOptions: PollOptions.FromString(optionsString),
            NumMembers: participants,
            IsAnonymous: isAnonymous,
            Type: style)
        );

        if (!Context.Interaction.HasResponded)
        {
            if (r.IsOK)
            {
                await EmbedXmlUtils
                    .CreateResponseEmbed("Poll created", $"Poll in channel {Context.Channel} was successfully created")
                    .RespondFromXmlAsync(Context);
            }
            else
            {
                await EmbedXmlUtils
                    .CreateErrorEmbed("Poll creation error", r.Error)
                    .RespondFromXmlAsync(Context, ephemeral: true);
            }
        }
    }

    [SlashCommand("clear", "Clear all polls in a current channel, without completion")]
    public async Task ClearPollAsync()
    {
        var r = await _botDispatcher.ExecuteAsync(Context, new ClearPollBotCommand());
        if (!Context.Interaction.HasResponded)
        {
            if (!r.IsOK)
            {
                await EmbedXmlUtils
                    .CreateErrorEmbed("Failed to clear polls", r.Error)
                    .RespondFromXmlAsync(Context, ephemeral: true);
            }
            else
            {
                await EmbedXmlUtils
                    .CreateResponseEmbed("Polls were deleted", "Deleted all polls from the current channel")
                    .RespondFromXmlAsync(Context);
            }
        }
    }

    [SlashCommand("complete", "Complete a poll in a current channel")]
    public async Task CompletePollAsync()
    {
        await RespondAsync("Computing...");
        var r = await _botDispatcher.ExecuteAsync(Context, new CompletePollBotCommand());
        await DeleteOriginalResponseAsync();
        
        if (!r.IsOK)
        {
            await EmbedXmlUtils
                .CreateErrorEmbed("Failed to complete poll", r.Error)
                .SendMessageFromXmlAsync(Context.Channel);
        }
    }
}