﻿using System.Text;
using Discord;
using Discord.Interactions;
using DiscordMultiBot.App.Data;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Utils;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace DiscordMultiBot.App.Commands;

public record CompletePollBotCommand() : ISocketBotCommand;
public record WritePollResultsBotCommand(PollType PollType, IEnumerable<PollVoteResultDto> Votes) : ISocketBotCommand;
public record ClearPollBotCommand() : ISocketBotCommand;
public record CreatePollBotCommand(PollOptions PollOptions, int NumMembers, bool IsAnonymous, string Type) : ISocketBotCommand;
public record MakePollVoteBotCommand(PollType Type, string Option, object Data) : ISocketBotCommand;
public record UpdatePollMessageBotCommand(PollDto Poll): ISocketBotCommand;

public sealed class CreatePollBotCommandHandler : ISocketBotCommandHandler<CreatePollBotCommand>
{
    private readonly ISocketBotCommandHandler<WritePollResultsBotCommand> _completeHandler;

    public CreatePollBotCommandHandler(
        ISocketBotCommandHandler<WritePollResultsBotCommand> completeHandler
    )
    {
        _completeHandler = completeHandler;
    }
    
    public async Task<ResultDto> ExecuteAsync(SocketInteractionContext context, CommandDispatcher dispatcher, CreatePollBotCommand command)
    {
        var pollType = Enum.Parse<PollType>(command.Type);

        if (command.PollOptions.Count == 1)
        {
            // check if it from template
            var getTemplateQuery = await dispatcher.QueryAsync(new GetPollOptionsTemplate(context.Guild.Id, command.PollOptions[0]));
            if (getTemplateQuery.IsOK)
            {
                var executed = await ExecuteAsync(context, dispatcher,
                    new CreatePollBotCommand(getTemplateQuery.Result, command.NumMembers, command.IsAnonymous, command.Type));
                return executed;
            }
            else
            {
                string voteData;
                switch (pollType)
                {
                    case PollType.Binary:
                        voteData = JsonConvert.SerializeObject(new PollDataYesNo(true));
                        break;
                    
                    case PollType.Numeric:
                        voteData = JsonConvert.SerializeObject(new PollDataPreference(3));
                        break;
                    
                    default:
                        return ResultDto.CreateError("Unknown poll type");
                        break;
                }
                
                var results = new List<PollVoteResultDto>();
                results.Add(new PollVoteResultDto(
                    PollId: 0,
                    ChannelId: context.Channel.Id,
                    UserId: context.User.Id,
                    VoteOption: command.PollOptions[0],
                    VoteData: voteData,
                    PollVoterState.Ready)
                );

                // Complete poll
                var executed = await _completeHandler.ExecuteAsync(context, dispatcher, new WritePollResultsBotCommand(
                    PollType: pollType,
                    Votes: results)
                );
                return executed;
            }
        }

        var createPollResult = await dispatcher.ExecuteAsync(new CreatePollCommand(
            ChannelId: context.Channel.Id,
            Style: command.Type,
            NumMembers: command.NumMembers,
            PollOptions: command.PollOptions,
            IsAnonymous: command.IsAnonymous
        ));

        if (!createPollResult.IsOK)
        {
            return ResultDto.CreateError(createPollResult.Error);
        }

        EmbedXmlCreator pollMessageBuilder = new();
        string pollLayoutName;
        switch (pollType)
        {
            case PollType.Binary:
                pollLayoutName = "PollBinary";
                break;
            
            case PollType.Numeric:
                pollLayoutName = "PollNumeric";
                break;
            
            default:
                return ResultDto.CreateError("Unknown poll type");
        }

        await context.Interaction.RespondAsync("Creating poll...");

        var m = await pollMessageBuilder
            .Create(pollLayoutName)
            .SendMessageFromXmlAsync(context.Channel);

        _ = context.Interaction.DeleteOriginalResponseAsync();
        _ = dispatcher.ExecuteAsync(new UpdatePollMetadataCommand(
            ChannelId: context.Channel.Id,
            MessageId: m.Id
        ));
        
        return ResultDto.CreateOK();
    }
}

public sealed class MakePollVoteCommandHandler : ISocketBotCommandHandler<MakePollVoteBotCommand>
{
    private readonly ISocketBotCommandHandler<UpdatePollMessageBotCommand> _updateCommand;

    public MakePollVoteCommandHandler(ISocketBotCommandHandler<UpdatePollMessageBotCommand> updateCommand)
    {
        _updateCommand = updateCommand;
    }
    
    public async Task<ResultDto> ExecuteAsync(SocketInteractionContext context, CommandDispatcher dispatcher, MakePollVoteBotCommand botCommand)
    {
        var pollQuery = await dispatcher.QueryAsync(new GetCurrentPollQuery(context.Channel.Id));
        if (!pollQuery.IsOK)
        {
            return ResultDto.CreateError(pollQuery.Error);
        }
        
        PollDto poll = pollQuery.Result;
        if (poll.Type != botCommand.Type)
        {
            if (context.Interaction.Type == InteractionType.ApplicationCommand)
            {
                var commandData = context.Interaction.Data as IUserCommandInteractionData;
                return ResultDto.CreateError($"`/{commandData!.Name}` is not allowed on current poll");
            }
        }

        if (!poll.Options.Contains(botCommand.Option))
        {
            return ResultDto.CreateError($"`{botCommand.Option}` is not a valid option for the current poll");
        }
            
        var addVote = await dispatcher.ExecuteAsync(new CreatePollVoteCommand(
            ChannelId: context.Channel.Id,
            UserId: context.User.Id,
            VoteOption: botCommand.Option,
            VoteData: JsonConvert.SerializeObject(botCommand.Data)
        ));

        if (!addVote.IsOK)
        {
            return ResultDto.CreateError(addVote.Error);
        }

        _ = _updateCommand.ExecuteAsync(context, dispatcher, new UpdatePollMessageBotCommand(poll));
        return ResultDto.CreateOK();
    }
}

public sealed class UpdatePollMessageCommandHandler : ISocketBotCommandHandler<UpdatePollMessageBotCommand>
{
    private void UpdateBinaryPollAsync(SocketInteractionContext context, PollDto poll, PollMetadataDto pollMetadata, IEnumerable<PollVoteResultDto> votes)
    {
        var xml = new EmbedXmlCreator();
        var resultEnumerable = votes
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
        _ = context.Channel.ModifyMessageAsync(pollMetadata.MessageId, props =>
        {
            props.Embeds = e.Embeds;
            props.Content = e.Text;
            props.Components = e.Comps;
        });
        
    }
    
    private void UpdateNumericPollAsync(SocketInteractionContext context, PollDto poll, PollMetadataDto pollMetadata, IEnumerable<PollVoteResultDto> votes)
    {
        var xml = new EmbedXmlCreator();
        var resultEnumerable = votes
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
        _ = context.Channel.ModifyMessageAsync(pollMetadata.MessageId, props =>
        {
            props.Embeds = e.Embeds;
            props.Content = e.Text;
        });
    }
    
    public async Task<ResultDto> ExecuteAsync(SocketInteractionContext context, CommandDispatcher dispatcher, UpdatePollMessageBotCommand botCommand)
    {
        var metadataQuery = await dispatcher.QueryAsync(new GetCurrentPollMetadata(context.Channel.Id));
        if (!metadataQuery.IsOK)
        {
            return ResultDto.CreateError(metadataQuery.Error);
        }

        var resultsQuery = await dispatcher.QueryAsync(new GetCurrentPollResults(context.Channel.Id));
        if (!resultsQuery.IsOK)
        {
            return ResultDto.CreateError(resultsQuery.Error);
        }

        switch (botCommand.Poll.Type)
        {
            case PollType.Binary:
                UpdateBinaryPollAsync(context, botCommand.Poll, metadataQuery.Result, resultsQuery.Result);
                return ResultDto.CreateOK();
            
            case PollType.Numeric:
                UpdateNumericPollAsync(context, botCommand.Poll, metadataQuery.Result, resultsQuery.Result);
                return ResultDto.CreateOK();
                
            default:
                return ResultDto.CreateError("Unknown poll type");
        }
    }
}

public sealed class CompletePollBotCommandHandler : ISocketBotCommandHandler<CompletePollBotCommand>
{
    private readonly ISocketBotCommandHandler<WritePollResultsBotCommand> _pollResultHandler;

    public CompletePollBotCommandHandler(ISocketBotCommandHandler<WritePollResultsBotCommand> pollResultHandler)
    {
        _pollResultHandler = pollResultHandler;
    }
    
    public async Task<ResultDto> ExecuteAsync(SocketInteractionContext context, CommandDispatcher dispatcher, CompletePollBotCommand command)
    {
        var resultsQuery = await dispatcher.QueryAsync(new GetCurrentPollResults(context.Channel.Id));
        var delPollCommand = await dispatcher.ExecuteAsync(new DeletePollCommand(context.Channel.Id));
        if (delPollCommand.IsOK && resultsQuery.IsOK)
        {
            PollDto poll = delPollCommand.Result;

            _ = context.Channel.DeleteMessageAsync(poll.Metadata!.MessageId);
            return await _pollResultHandler.ExecuteAsync(context, dispatcher, new WritePollResultsBotCommand(poll.Type, resultsQuery.Result));
        }
        else
        {
            if (!delPollCommand.IsOK) { return ResultDto.CreateError(delPollCommand.Error); }
            if (!resultsQuery.IsOK) { return ResultDto.CreateError(resultsQuery.Error); }
            throw new InvalidProgramException();
        }
    }
}

public sealed class ClearPollCommandHandler : ISocketBotCommandHandler<ClearPollBotCommand>
{
    public async Task<ResultDto> ExecuteAsync(SocketInteractionContext context, CommandDispatcher dispatcher, ClearPollBotCommand botCommand)
    {
        var r = await dispatcher.ExecuteAsync(new DeletePollCommand(context.Channel.Id));
        if (r.IsOK)
        {
            if (r.Result.Metadata is not null)
            {
                _ = context.Channel
                    .DeleteMessageAsync(r.Result.Metadata.MessageId);
            }
            
            return ResultDto.CreateOK();
        }
        else
        {
            return ResultDto.CreateError(r.Error);
        }
    }
}

public sealed class WritePollResultsCommandHandler : ISocketBotCommandHandler<WritePollResultsBotCommand>
{
    private async Task<ResultDto> WriteBinaryPollAsync(SocketInteractionContext context, CommandDispatcher dispatcher,
        WritePollResultsBotCommand botCommand)
    {
        var resultList = botCommand.Votes
            .Select(x => (Vote: x, Data: JsonConvert.DeserializeObject<PollDataYesNo>(x.VoteData)!))
            .ToList();
        
        if (resultList.Count == 0)
        {
            EmbedXmlCreator err = new EmbedXmlCreator();
            err.Bindings.Add("Title", "Nothing was voted");
            EmbedXmlDoc errEmbed = err.Create("Error");

            await context.Channel.SendMessageAsync(text: errEmbed.Text, embeds: errEmbed.Embeds);
            return ResultDto.CreateOK();
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
            var votedUsers = String.Join("\n", resultGroup.Items.Select(x => $"{context.Guild.GetUser(x.Vote.UserId).Username} ({(x.Data.Value ? "Yes" : "No")})"));

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
        string colorHex = "";
        optionXml.Bindings.Add("Option", option);
        {
            try
            {
                IConfigurationSection colorsConfiguration =
                    DiscordMultiBot.Instance.Configuration
                        .GetSection("Bot:PollVotes")
                        .GetSection(option);
                
                colorHex = colorsConfiguration["Color"] ?? "";
            }
            catch (Exception)
            {
                // ignore
            }

            if (colorHex.Length == 0)
            {
                colorHex = "ffaabb"; // default
            }
        }
        optionXml.Bindings.Add("Color", colorHex);
        
        EmbedXmlDoc optionEmbed = optionXml.Create("PollOption");
        EmbedXmlDoc optionsViewEmbed = resultXml.Create("PollResult");

        await context.Channel.SendMessageAsync(text: optionEmbed.Text, embeds: optionEmbed.Embeds);
        await context.Channel.SendMessageAsync(text: optionsViewEmbed.Text, embeds: optionsViewEmbed.Embeds);
        return ResultDto.CreateOK();
    }

    private async Task<ResultDto> WriteNumericPollAsync(SocketInteractionContext context, CommandDispatcher dispatcher,
        WritePollResultsBotCommand botCommand)
    {
        var resultList = botCommand.Votes
            .Select(x => (Vote: x, Data: JsonConvert.DeserializeObject<PollDataPreference>(x.VoteData)!))
            .ToList();
        
        if (resultList.Count == 0)
        {
            EmbedXmlCreator err = new EmbedXmlCreator();
            err.Bindings.Add("Title", "Nothing was voted");
            EmbedXmlDoc errEmbed = err.Create("Error");

            await context.Channel.SendMessageAsync(text: errEmbed.Text, embeds: errEmbed.Embeds);
            return ResultDto.CreateOK();
        }

        var resultGroups = resultList
            .GroupBy(x => x.Vote.VoteOption)
            .Select(x => (x.Key, Items: x.AsEnumerable(), Sum: x.Sum(y => y.Data.Preference)))
            .OrderByDescending(x => x.Sum)
            .ToList();

        EmbedXmlCreator resultXml = new();

        foreach (var resultGroup in resultGroups)
        {
            var votedUsers = String.Join("\n", resultGroup.Items.Select(x => $"{context.Guild.GetUser(x.Vote.UserId).Username} ({x.Data.Preference})"));
            resultXml.Fields.Add(new EmbedXmlField($"`{resultGroup.Key}` - {resultGroup.Sum}", votedUsers));
        }

        string option = resultGroups
            .TakeWhile(x => x.Sum == resultGroups[0].Sum)
            .MaxBy(_ => Random.Shared.Next())
            .Key;

        var optionXml = new EmbedXmlCreator();
        optionXml.Bindings.Add("Option", option);
        string colorHex = "";
        {
            try
            {
                IConfigurationSection colorsConfiguration = DiscordMultiBot.Instance.Configuration
                    .GetSection("Bot:PollVotes")
                    .GetSection(option);
                
                colorHex = colorsConfiguration["Color"] ?? "";
            }
            catch (Exception)
            {
                // ignore
            }

            if (colorHex.Length == 0)
            {
                colorHex = "ffaabb"; // default
            }
        }
        optionXml.Bindings.Add("Color", colorHex);
        
        EmbedXmlDoc optionEmbed = optionXml.Create("PollOption");
        EmbedXmlDoc optionsViewEmbed = resultXml.Create("PollResult");

        await context.Channel.SendMessageAsync(text: optionEmbed.Text, embeds: optionEmbed.Embeds);
        await context.Channel.SendMessageAsync(text: optionsViewEmbed.Text, embeds: optionsViewEmbed.Embeds);
        return ResultDto.CreateOK();
    }
    
    public async Task<ResultDto> ExecuteAsync(SocketInteractionContext context, CommandDispatcher dispatcher, WritePollResultsBotCommand botCommand)
    {
        ResultDto result;
        switch (botCommand.PollType)
        {
            case PollType.Binary:
                result = await WriteBinaryPollAsync(context, dispatcher, botCommand);
                break;
            
            case PollType.Numeric:
                result = await WriteNumericPollAsync(context, dispatcher, botCommand);
                break;
            
            default:
                result = ResultDto.CreateError("Unknown poll type");
                break;
        }

        _ = context.Interaction.DeleteOriginalResponseAsync();
        return result;
    }
}