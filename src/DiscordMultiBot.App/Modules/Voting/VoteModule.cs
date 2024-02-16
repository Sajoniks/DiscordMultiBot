using Discord;
using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.App.Modules.Voting;


[Group("vote", "Vote in a poll that is running in the current channel")]
public partial class VoteModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CommandDispatcher _dispatcher;

    internal class PollOptionsAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
            IParameterInfo parameter, IServiceProvider services)
        {
            var dispatcher = services.GetRequiredService<CommandDispatcher>();
            var poll = await dispatcher.QueryAsync<GetCurrentPollQuery, PollDto>(
                new GetCurrentPollQuery(context.Channel.Id));

            if (poll.IsOK)
            {
                var results = poll.Result!.Options
                    .Select(x => new AutocompleteResult(x, x));
                
                return AutocompletionResult.FromSuccess(results);
            }
            else
            {
                return AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, poll.Error);
            }
        }
    }
    
    public VoteModule(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [SlashCommand("revote", "Use your last poll values in current poll", ignoreGroupNames: true)]
    public async Task RevoteAsync()
    {
        
    }
}