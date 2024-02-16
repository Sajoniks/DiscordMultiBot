using System.Text;
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
    [SlashCommand("pref", "Vote using preference mode")]
    public async Task VotePreferenceAsync(
        [Autocomplete(typeof(PollOptionsAutocompleteHandler)), Summary("Option", "Your option")] string option,
        [Summary("Pref", "Preference value"), MinValue(-3), MaxValue(3)] int preference
    )
    {
        var pollQuery = await _dispatcher.QueryAsync<GetCurrentPollQuery, PollDto>(new GetCurrentPollQuery(Context.Channel.Id));
        if (pollQuery.IsOK)
        {
            var poll = pollQuery.Result!;
            if (poll.Type != PollType.Numeric)
            {
                EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Vote failed",
                    "`/vote pref` is not applicable to the current poll");
                await RespondAsync(embeds: e.Embeds, text: e.Text, ephemeral: true);
                return;
            }

            var addVote = await _dispatcher.ExecuteAsync(new CreatePollVoteCommand(
                ChannelId: Context.Channel.Id,
                UserId: Context.User.Id,
                VoteOption: option,
                VoteData: JsonConvert.SerializeObject(new PollDataPreference(preference))
            )
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
                            .Select(x => (Result: x, Data: JsonConvert.DeserializeObject<PollDataPreference>(x.VoteData)!))
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
                                    .Sum(x => x.Data.Preference);
                                
                                sb.AppendFormat(" - `{0}`", sumVotes);
                            }
                            
                            xml.Fields.Add(new EmbedXmlField(sb.ToString(), "\u200b"));
                            ++groupingIdx;
                        }

                        EmbedXmlDoc e = xml.Create("PollNumeric");
                        _ = Context.Channel.ModifyMessageAsync(pm.MessageId, props =>
                        {
                            props.Embeds = e.Embeds;
                            props.Content = e.Text;
                        });
                    }
                }

                EmbedXmlDoc responseXml = EmbedXmlUtils.CreateResponseEmbed("Vote accepted", $"You have voted for `{option}`");
                await RespondAsync(embeds: responseXml.Embeds, text: responseXml.Text, ephemeral: true);
                return;
            }
            else
            {
                EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Vote failed", addVote.Error);
                await RespondAsync(embeds: e.Embeds, text: e.Text, ephemeral: true);
                return;
            }
        }
        else
        {
            EmbedXmlDoc e = EmbedXmlUtils.CreateErrorEmbed("Vote failed", pollQuery.Error);
            await RespondAsync(embeds: e.Embeds, text: e.Text, ephemeral: true);
            return;
        }
    }
}