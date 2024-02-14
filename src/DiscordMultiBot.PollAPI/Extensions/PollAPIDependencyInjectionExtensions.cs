using DiscordMultiBot.PollService.Data.Connection;
using DiscordMultiBot.PollService.Data.Template;
using DiscordMultiBot.PollService.Repository;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.PollService.Extensions;

public static class PollApiDependencyInjectionExtensions
{
    public static IServiceCollection AddPollApi(this IServiceCollection collection, string connectionString)
    {
        collection
            .AddTransient<DbPoll>(x => new DbPoll(new DataOptions().UseSQLite(connectionString)))
            .AddTransient<IVoteRepository, VoteRepository>()
            .AddTransient<IPollRepository, PollRepository>()
            .AddTransient<IVoteTemplateRepository>();

        return collection;
    }
}