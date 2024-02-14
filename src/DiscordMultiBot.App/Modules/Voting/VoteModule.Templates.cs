
using Discord.Interactions;

namespace DiscordMultiBot.App.Modules.Voting;

public partial class VoteModule
{
    [Group("template", "Work with voting templates")]
    public class TemplateModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("add", "Add new template")]
        public async Task AddTemplateAsync()
        {
            
        }

        [SlashCommand("remove", "Remove existing template")]
        public async Task RemoveTemplateAsync()
        {
            
        }
    }
}