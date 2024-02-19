using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.Data;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.App.Modules.Voting;

public partial class VoteModule
{
    [SlashCommand("yesno", "Vote using Yes or No options")]
    public async Task VoteAsync(
        [Autocomplete(typeof(PollOptionsAutocompleteHandler)), Summary("option", "Poll option")] string option, 
        [Choice("Yes", "true"), Choice("No", "false"), Summary("choice", "Your vote")] string choice
    )
    {
        var r = await _botDispatcher.ExecuteAsync(
            Context,
            new MakePollVoteBotCommand(PollType.Binary, option, new PollDataYesNo(Convert.ToBoolean(choice)))
        );

        if (r.IsOK)
        {
            await EmbedXmlUtils
                .CreateResponseEmbed("Vote accepted", $"You have voted for `{option}` as `{(Convert.ToBoolean(choice) ? "Yes" : "No")}`")
                .RespondFromXmlAsync(Context, ephemeral: true);
        }
        else
        {
            await EmbedXmlUtils
                .CreateErrorEmbed("Vote failed", r.Error)
                .RespondFromXmlAsync(Context, ephemeral: true);
        }
    }
}