using DiscordMultiBot.App.Commands;
using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.App.Extensions;

public static class DiscordMultiBotCommandDependencyInjectionExtensions
{
    public static IServiceCollection AddDiscordMultiBotCommands(this IServiceCollection collection)
    {
        // Commands
        collection
            .AddTransient<ICommandHandler<CreatePollCommand, PollDto>, CreatePollCommandHandler>()
            .AddTransient<ICommandHandler<DeletePollCommand, PollDto>, DeletePollCommandHandler>()
            .AddTransient<ICommandHandler<UpdatePollVoterStateCommand, PollVoterStateDto>, UpdatePollVoterStateCommandHandler>()
            .AddTransient<ICommandHandler<UpdatePollMetadataCommand>, UpdatePollMetadataCommandHandler>()
            .AddTransient<ICommandHandler<CreatePollVoteCommand>, CreateVoteCommandHandler>();

        // Queries
        collection
            .AddTransient<IQueryHandler<GetCurrentPollQuery, PollDto>, GetCurrentPollQueryHandler>()
            .AddTransient<IQueryHandler<GetCurrentPollMetadata, PollMetadataDto>, GetCurrentPollMetadataQueryHandler>()
            .AddTransient<IQueryHandler<GetCurrentPollResults, IEnumerable<PollVoteResultDto>>, GetCurrentPollResultsQueryHandler>();
            
        return collection;
    }
}