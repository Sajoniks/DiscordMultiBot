using Discord.Interactions;
using DiscordMultiBot.PollService.Command;

namespace DiscordMultiBot.App.Modules.Voting;


[Group("vote", "Vote in a poll that is running in the current channel")]
public partial class VoteModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CommandDispatcher _dispatcher;

    public VoteModule(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [SlashCommand("revote", "Use your last poll values in current poll", ignoreGroupNames: true)]
    public async Task RevoteAsync()
    {
        
    }
}