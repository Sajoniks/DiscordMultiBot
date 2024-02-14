using Discord.Interactions;

namespace DiscordMultiBot.App.Modules.Misc;

public class MiscModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("roll", "Do a roll of a dice")]
    public async Task RollDiceAsync()
    {
        
    }
}