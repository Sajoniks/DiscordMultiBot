using System.Text;
using Discord;
using Discord.Interactions;
using DiscordMultiBot.App.Commands;
using DiscordMultiBot.App.Data;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Utils;
using DiscordMultiBot.PollService.Data.Dto;
using Newtonsoft.Json;

namespace DiscordMultiBot.App.Modules.Voting;

public partial class VoteModule
{
    [SlashCommand("yesno", "Vote using Yes or No options")]
    public async Task VoteAsync(
        [Autocomplete(typeof(PollOptionsAutocompleteHandler)), Summary("option", "Poll option")] string option, 
        [Choice("Yes", "true"), Choice("No", "false"), Summary("choice", "Your vote")] string choice
    )
    {
        var pollQuery = await _dispatcher.QueryAsync<GetCurrentPollQuery, PollDto>(new GetCurrentPollQuery(Context.Channel.Id));
        if (pollQuery.IsOK)
        {
            PollDto poll = pollQuery.Result!;
            if (poll.Type != PollType.Binary)
            {
                EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Vote failed", "`/vote yesno` is not appicable to the current poll");
                await RespondAsync(embeds: e.Embeds, text: e.Text, components: e.Comps, ephemeral: true);
                return;
            }

            if (!poll.Options.Contains(option))
            {
                EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Vote failed",
                    $"`{option}` is not a valid option for current poll");
                await RespondAsync(embeds: e.Embeds, text: e.Text, components: e.Comps, ephemeral: true);
                return;
            }

            var addVote = await _dispatcher.ExecuteAsync(new CreatePollVoteCommand(
                ChannelId: Context.Channel.Id,
                UserId: Context.User.Id,
                VoteOption: option,
                VoteData: JsonConvert.SerializeObject(new PollDataYesNo(Convert.ToBoolean(choice))))
            );
            
            if (addVote.IsOK)
            {
                // @todo
                // queue update for the poll message

                var getMetadata = await _dispatcher.QueryAsync<GetCurrentPollMetadata, PollMetadataDto>(
                    new GetCurrentPollMetadata(Context.Channel.Id));

                if (getMetadata.IsOK)
                {
                    var pm = getMetadata.Result!;
                    var xml = new EmbedXmlCreator();
                    var results =
                        await _dispatcher.QueryAsync<GetCurrentPollResults, IEnumerable<PollVoteResultDto>>(
                            new GetCurrentPollResults(Context.Channel.Id));

                    if (results.IsOK)
                    {
                        var resultEnumerable = results.Result!
                            .Select(x => (Result: x, Data: JsonConvert.DeserializeObject<PollDataYesNo>(x.VoteData)!))
                            .GroupBy(kv => kv.Result.VoteOption)
                            .ToList();

                        uint groupingIdx = 1;
                        foreach (var resultGroup in resultEnumerable)
                        {
                            var sb = new StringBuilder();
                            sb.AppendFormat("{0}`{1}`", StringUtils.ConvertUInt32ToEmoji(groupingIdx), resultGroup.Key);
                            if (!poll.IsAnonymous)
                            {
                                int sumVotes = resultGroup
                                    .Sum(x => x.Data.Value ? 1 : -1);
                                
                                sb.AppendFormat(" - `{0}`", sumVotes);
                            }
                            
                            xml.Fields.Add(new EmbedXmlField(sb.ToString(), "\u200b"));
                            ++groupingIdx;
                        }

                        EmbedXmlDoc e = xml.Create("PollBinary");
                        _ = Context.Channel.ModifyMessageAsync(pm.MessageId, props =>
                        {
                            props.Embeds = e.Embeds;
                            props.Content = e.Text;
                            props.Components = e.Comps;
                        });
                    }
                }

                EmbedXmlDoc responseXml = EmbedXmlUtils.CreateResponseEmbed("Vote accepted", $"You have voted for `{option}` as `{(choice.Equals("true") ? "Yes" : "No")}`");
                await RespondAsync(embeds: responseXml.Embeds, components: responseXml.Comps, text: responseXml.Text, ephemeral: true);
                return;
            }
            else
            {
                EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Vote failed", addVote.Error);
                await RespondAsync(embeds: e.Embeds, components: e.Comps, text: e.Text, ephemeral: true);
                return;
            }
        }
        else
        {
            EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Vote failed", pollQuery.Error);
            await RespondAsync(embeds: e.Embeds, components: e.Comps, text: e.Text, ephemeral: true);
            return;
        }
    }
}