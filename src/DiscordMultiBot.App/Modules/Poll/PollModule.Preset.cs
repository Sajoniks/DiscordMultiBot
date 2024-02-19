using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.App.Modules.Poll;

public partial class PollModule
{
    
    [Group("preset", "Preset for polls")]
    public class PresetModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly BotCommandDispatcher _botCommandDispatcher;
        private readonly CommandDispatcher _commandDispatcher;

        public PresetModule(BotCommandDispatcher botCommandDispatcher, CommandDispatcher commandDispatcher)
        {
            _botCommandDispatcher = botCommandDispatcher;
            _commandDispatcher = commandDispatcher;
        }

        [SlashCommand("delete", "Delete poll template")]
        public async Task DeleteTemplateAsync(
            [Summary("Name", "Name for the template")] string name
        )
        {
            var r = await _commandDispatcher.ExecuteAsync(new DeletePollOptionsTemplateCommand(Context.Guild.Id, name));
            if (r.IsOK)
            {
                await EmbedXmlUtils
                    .CreateResponseEmbed("Template deleted", $"Template `{name}` was deleted successfully")
                    .RespondFromXmlAsync(Context, ephemeral: true);
            }
            else
            {
                await EmbedXmlUtils
                    .CreateErrorEmbed("Template deletion error", r.Error)
                    .RespondFromXmlAsync(Context, ephemeral: true);
            }
        }
        
        [SlashCommand("create", "Create poll template")]
        public async Task CreateTemplateAsync(
            [Summary("Options", "List of options separated with ++")] string options,
            [Summary("Name", "Name for the template")] string name
        )
        {
            var r= await _commandDispatcher.ExecuteAsync(
                new CreatePollOptionsTemplateCommand(Context.Guild.Id, name, PollOptions.FromString(options)));

            if (r.IsOK)
            {
                await EmbedXmlUtils
                    .CreateResponseEmbed("Template created", $"Template `{name}` was created successfully")
                    .RespondFromXmlAsync(Context, ephemeral: true);
            }
            else
            {
                await EmbedXmlUtils
                    .CreateErrorEmbed("Template creation error", r.Error)
                    .RespondFromXmlAsync(Context, ephemeral: true);
            }
        }
    }
}