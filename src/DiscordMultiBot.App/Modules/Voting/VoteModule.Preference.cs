using Discord.Interactions;

namespace DiscordMultiBot.App.Modules.Voting;


public partial class VoteModule
{
    [Group("pref", "Vote using preference option")]
    public class PreferenceModule : InteractionModuleBase<SocketInteractionContext>
    {
        
    }
}