using System.Text;
using DiscordMultiBot.App.Data;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.PollService.Data.Dto;
using Newtonsoft.Json;

namespace DiscordMultiBot.App.Modules.Poll;

public partial class PollModule
{
    internal async Task RespondBinaryPollCompletedAsync(PollDto poll, IEnumerable<PollVoteResultDto> votes)
    {
        var resultList = votes
            .Select(x => (Vote: x, Data: JsonConvert.DeserializeObject<PollDataYesNo>(x.VoteData)!))
            .ToList();
        
        if (resultList.Count == 0)
        {
            EmbedXmlCreator err = new EmbedXmlCreator();
            err.Bindings.Add("Title", "Nothing was voted");
            EmbedXmlDoc errEmbed = err.Create("Error");

            await Context.Channel.SendMessageAsync(text: errEmbed.Text, embeds: errEmbed.Embeds);
            return;
        }
        
        var resultGroups = resultList.GroupBy(x => x.Vote.VoteOption);

        SortedDictionary<string, int> optionCounts = new();
        EmbedXmlCreator resultXml = new();

        foreach (var resultGroup in resultGroups)
        {
            var votedUsers = String.Join(", ", resultGroup
                .Select(x => (User: Context.Guild.GetUser(x.Vote.UserId), Data: x.Data))
                .Select(x => $"{x.User.DisplayName} (@{x.User.Username}) - {(x.Data.Value ? "Yes" : "No")}"));
            
            resultXml.Fields.Add(new EmbedXmlField($"`{resultGroup.Key}`", votedUsers));

            optionCounts.TryAdd(resultGroup.Key, 0);
            optionCounts[resultGroup.Key] = resultGroup.Sum(x => x.Data.Value ? 1 : -1);
        }

        var option = optionCounts
            .TakeWhile(x => x.Value == optionCounts.First().Value)
            .MaxBy(_ => Random.Shared.Next());

        var optionXml = new EmbedXmlCreator();
        optionXml.Bindings.Add("Option", option.Key);
        optionXml.Bindings.Add("Color", "ffaabb");
        
        EmbedXmlDoc optionEmbed = optionXml.Create("PollOption");
        EmbedXmlDoc optionsViewEmbed = resultXml.Create("PollResult");

        await Context.Channel.SendMessageAsync(text: optionEmbed.Text, embeds: optionEmbed.Embeds);
        await Context.Channel.SendMessageAsync(text: optionsViewEmbed.Text, embeds: optionsViewEmbed.Embeds);
    }

    internal async Task RespondHandleNumericPollCompletedAsync(PollDto poll, IEnumerable<PollVoteResultDto> votes)
    {
        var resultList = votes
            .Select(x => (Vote: x, Data: JsonConvert.DeserializeObject<PollDataPreference>(x.VoteData)!))
            .ToList();
        
        if (resultList.Count == 0)
        {
            EmbedXmlCreator err = new EmbedXmlCreator();
            err.Bindings.Add("Title", "Nothing was voted");
            EmbedXmlDoc errEmbed = err.Create("Error");

            await Context.Channel.SendMessageAsync(text: errEmbed.Text, embeds: errEmbed.Embeds);
            return;
        }
        
        var resultGroups = resultList.GroupBy(x => x.Vote.VoteOption);

        SortedDictionary<string, int> optionCounts = new();
        EmbedXmlCreator resultXml = new();

        foreach (var resultGroup in resultGroups)
        {
            var votedUsers = String.Join(", ", resultGroup
                .Select(x => (User: Context.Guild.GetUser(x.Vote.UserId), Data: x.Data))
                .Select(x => $"{x.User.DisplayName} (@{x.User.Username}) - {x.Data.Preference}"));
            
            resultXml.Fields.Add(new EmbedXmlField($"`{resultGroup.Key}`", votedUsers));

            optionCounts.TryAdd(resultGroup.Key, 0);
            optionCounts[resultGroup.Key] = resultGroup.Sum(x => x.Data.Preference);
        }

        var option = optionCounts
            .TakeWhile(x => x.Value == optionCounts.First().Value)
            .MaxBy(_ => Random.Shared.Next());

        var optionXml = new EmbedXmlCreator();
        optionXml.Bindings.Add("Option", option.Key);
        optionXml.Bindings.Add("Color", "ffaabb");
        
        EmbedXmlDoc optionEmbed = optionXml.Create("PollOption");
        EmbedXmlDoc optionsViewEmbed = resultXml.Create("PollResult");

        await Context.Channel.SendMessageAsync(text: optionEmbed.Text, embeds: optionEmbed.Embeds);
        await Context.Channel.SendMessageAsync(text: optionsViewEmbed.Text, embeds: optionsViewEmbed.Embeds);
    }
}