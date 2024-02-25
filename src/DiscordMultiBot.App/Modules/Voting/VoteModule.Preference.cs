using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.Data;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.App.Modules.Voting;


public partial class VoteModule
{
    [SlashCommand("pref", "Vote using preference mode")]
    public async Task VotePreferenceAsync(
        [Autocomplete(typeof(PollOptionsAutocompleteHandler)), Summary("Option", "Your option")] string option,
        [Summary("Pref", "Preference value"), MinValue(-3), MaxValue(3)] int preference
    )
    {
        var r = await _botDispatcher.ExecuteAsync(
            Context,
            new MakePollVoteBotCommand(PollType.Numeric, option, new PollDataPreference(preference))
        );

        if (r.IsOK)
        {
            await EmbedXmlUtils
                .CreateResponseEmbed("Vote accepted", $"You have voted for `{option}` as `{preference}`")
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