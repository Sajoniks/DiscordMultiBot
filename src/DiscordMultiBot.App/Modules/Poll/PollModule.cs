using Discord.Interactions;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.App.Modules.Poll;

public class PollModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CommandDispatcher _dispatcher;
    
    public PollModule(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }
    
    [SlashCommand("poll", "Start a poll in current channel")]
    public async Task PollAsync([Summary("options", "List of vote parameters separated with \"++\"")] string optionsString)
    {
        var opts = PollOptions.FromString(optionsString);
        var p = await _dispatcher.ExecuteAsync<PollCommand, PollDto?>(new PollCommand(PollCommandAction.Create, opts));
        if (p is null)
        {
            await RespondAsync(ephemeral: true);
        }
        else
        {
            
        }
    }
}