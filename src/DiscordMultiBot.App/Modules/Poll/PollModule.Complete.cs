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

        var resultGroups = resultList
            .GroupBy(x => x.Vote.VoteOption)
            .Select(x => (x.Key, Items: x.AsEnumerable(), Yes: x.Count(y => y.Data.Value),
                No: x.Count(y => !y.Data.Value)))
            .OrderByDescending(x => x.Yes - x.No)
            .ToList();
        
        EmbedXmlCreator resultXml = new();

        foreach (var resultGroup in resultGroups)
        {
            var votedUsers = String.Join("\n", resultGroup.Items.Select(x => $"{Context.Guild.GetUser(x.Vote.UserId).Username} ({(x.Data.Value ? "Yes" : "No")})"));

            var resultKeySb = new StringBuilder();
            resultKeySb.AppendFormat("`{0}` - ", resultGroup.Key);
            if (resultGroup.Yes != 0)
            {
                resultKeySb.AppendFormat("{0} Yes", resultGroup.Yes);
                if (resultGroup.No != 0)
                {
                    resultKeySb.Append(", ");
                }
            }

            if (resultGroup.No != 0)
            {
                resultKeySb.AppendFormat("{0} No", resultGroup.No);
            }

            resultXml.Fields.Add(new EmbedXmlField(resultKeySb.ToString(), votedUsers));
        }

        string option = resultGroups
            .TakeWhile(x => (x.Yes - x.No) == (resultGroups[0].Yes - resultGroups[0].No))
            .MaxBy(_ => Random.Shared.Next())
            .Key;

        var optionXml = new EmbedXmlCreator();
        optionXml.Bindings.Add("Option", option);
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

        var resultGroups = resultList
            .GroupBy(x => x.Vote.VoteOption)
            .Select(x => (x.Key, Items: x.AsEnumerable(), Sum: x.Sum(y => y.Data.Preference)))
            .OrderByDescending(x => x.Sum)
            .ToList();

        EmbedXmlCreator resultXml = new();

        foreach (var resultGroup in resultGroups)
        {
            var votedUsers = String.Join("\n", resultGroup.Items.Select(x => $"{Context.Guild.GetUser(x.Vote.UserId).Username} ({x.Data.Preference})"));
            resultXml.Fields.Add(new EmbedXmlField($"`{resultGroup.Key}` - {resultGroup.Sum}", votedUsers));
        }

        string option = resultGroups
            .TakeWhile(x => x.Sum == resultGroups[0].Sum)
            .MaxBy(_ => Random.Shared.Next())
            .Key;

        var optionXml = new EmbedXmlCreator();
        optionXml.Bindings.Add("Option", option);
        optionXml.Bindings.Add("Color", "ffaabb");
        
        EmbedXmlDoc optionEmbed = optionXml.Create("PollOption");
        EmbedXmlDoc optionsViewEmbed = resultXml.Create("PollResult");

        await Context.Channel.SendMessageAsync(text: optionEmbed.Text, embeds: optionEmbed.Embeds);
        await Context.Channel.SendMessageAsync(text: optionsViewEmbed.Text, embeds: optionsViewEmbed.Embeds);
    }
}