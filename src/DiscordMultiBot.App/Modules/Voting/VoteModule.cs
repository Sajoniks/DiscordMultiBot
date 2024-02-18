using Discord;
using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.EmbedXml;
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

    [ComponentInteraction("vote:update-state", ignoreGroupNames: true, TreatAsRegex = false)]
    public async Task OnSetVoterStateAsync()
    {
        var getPollQuery = new GetCurrentPollQuery(Context.Channel.Id);
        var getPoll = await _dispatcher.QueryAsync<GetCurrentPollQuery, PollDto>(getPollQuery);

        EmbedXmlDoc response;

        if (getPoll.IsOK)
        {
            var poll = getPoll.Result!;
            
            var updVoterStateCommand = new UpdatePollVoterStateCommand(Context.Channel.Id, Context.User.Id);
            var updMetadata =
                await _dispatcher.ExecuteAsync<UpdatePollVoterStateCommand, PollVoterStateDto>(updVoterStateCommand);

            if (updMetadata.IsOK)
            {
                PollVoterStateDto state = updMetadata.Result!;
                string message = "";
                switch (state.VoterState)
                {
                    case PollVoterState.Ready:
                        message = "You are now ready!";
                        break;
                    
                    case PollVoterState.NotReady:
                        message = "You are now not ready!";
                        break;
                }
                
                response = EmbedXmlUtils.CreateResponseEmbed("Accepted", message);

                int numReady = (await _dispatcher.QueryAsync<GetNumVotes, int>(new GetNumVotes(Context.Channel.Id)))
                    .Result;
             
                
            }
            else
            {
                response = EmbedXmlUtils.CreateErrorEmbed("Error", updMetadata.Error);
            }
        }
        else
        {
            response = EmbedXmlUtils.CreateErrorEmbed("Error", getPoll.Error);
        }

        await RespondAsync(text: response.Text, components: response.Comps, embeds: response.Embeds);
    }
    
    [SlashCommand("revote", "Use your last poll values in current poll", ignoreGroupNames: true)]
    public async Task RevoteAsync()
    {
        
    }
}