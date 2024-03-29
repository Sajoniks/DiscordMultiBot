﻿using Discord;
using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Utils;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.App.Modules.Voting;


[Group("vote", "Vote in a poll that is running in the current channel")]
public partial class VoteModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CommandDispatcher _dispatcher;
    private readonly BotCommandDispatcher _botDispatcher;

    internal class PollOptionsAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
            IParameterInfo parameter, IServiceProvider services)
        {
            var dispatcher = services.GetRequiredService<CommandDispatcher>();
            var poll = await dispatcher.QueryAsync(new GetCurrentPollQuery(context.Channel.Id));

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
    
    public VoteModule(CommandDispatcher dispatcher, BotCommandDispatcher botDispatcher)
    {
        _dispatcher = dispatcher;
        _botDispatcher = botDispatcher;
    }

    [ComponentInteraction("vote:update-state", ignoreGroupNames: true, TreatAsRegex = false)]
    public async Task OnSetVoterStateAsync()
    {
        var getPollQuery = new GetCurrentPollQuery(Context.Channel.Id);
        var getPoll = await _dispatcher.QueryAsync(getPollQuery);

        EmbedXmlDoc response;

        if (getPoll.IsOK)
        {
            var poll = getPoll.Result!;
            
            var updVoterStateCommand = new UpdatePollVoterStateCommand(Context.Channel.Id, Context.User.Id);
            var updMetadata = await _dispatcher.ExecuteAsync(updVoterStateCommand);

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

                var numReadyQuery = await _dispatcher.QueryAsync(new GetNumVotes(Context.Channel.Id));
                if (numReadyQuery.IsOK && numReadyQuery.Result == poll.NumMembers)
                {
                    await _botDispatcher.ExecuteAsync(Context, new CompletePollBotCommand());
                }
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

        await RespondAsync(text: response.Text, components: response.Comps, embeds: response.Embeds, ephemeral: true);
    }
    
    [SlashCommand("revote", "Use your last poll values in current poll", ignoreGroupNames: true)]
    public async Task RevoteAsync()
    {
        var poll = await _dispatcher.QueryAsync(new GetCurrentPollQuery(Context.Channel.Id));
        if (!poll.IsOK)
        {
            return;
        }
        
        var addVotesCommand = await _dispatcher.ExecuteAsync(new CreatePollVotesFromHistoryCommand(Context.Channel.Id, Context.User.Id));
        if (addVotesCommand.IsOK)
        {
            await EmbedXmlUtils
                    .CreateResponseEmbed("Revote applied", $"Options ({addVotesCommand.Result.Count()}) from the previous poll were applied",
                        (x) =>
                        {
                            foreach (var vote in addVotesCommand.Result)
                            {
                                x.Fields.Add(new EmbedXmlField(
                                    Name: vote.VoteOption, 
                                    Value: PollUtils.PollVoteDataByTypeToString(vote.VoteData, poll.Result.Type), 
                                    Inline: true
                                ));
                            }
                        })
                    .RespondFromXmlAsync(Context, ephemeral: true);

            _ = _botDispatcher.ExecuteAsync(Context, new UpdatePollMessageBotCommand(poll.Result));
        }
        else
        {
            await EmbedXmlUtils
                .CreateErrorEmbed("Error", addVotesCommand.Error)
                .RespondFromXmlAsync(Context, ephemeral: true);
        }
    }
}