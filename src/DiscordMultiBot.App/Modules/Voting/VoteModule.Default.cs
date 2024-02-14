using Discord.Interactions;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.App.Modules.Voting;

public partial class VoteModule
{
    [SlashCommand("vote", "Vote using Yes or No options", ignoreGroupNames: true)]
    public async Task VoteAsync()
    {
    }
}