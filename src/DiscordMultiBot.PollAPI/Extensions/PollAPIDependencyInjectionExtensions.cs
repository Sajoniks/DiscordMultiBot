using DiscordMultiBot.PollService.Command;
using DiscordMultiBot.PollService.Data.Connection;
using DiscordMultiBot.PollService.Repository;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiscordMultiBot.PollService.Extensions;

public static class PollApiDependencyInjectionExtensions
{
    public static IServiceCollection AddPollApiRepositories(this IServiceCollection collection, string connectionString, Action<IServiceCollection> dispatcherServices)
    {
        collection
            .AddTransient<DbPoll>(x => new DbPoll(new DataOptions().UseSQLite(connectionString)))
            .AddTransient<IPollRepository, PollRepository>()
            .AddTransient<IVoteTemplateRepository, VoteTemplateRepository>()
            .AddTransient(x =>
            {
                var innerDispatcherServices = new ServiceCollection();
                innerDispatcherServices.Add(collection);
                dispatcherServices(innerDispatcherServices);

                return new CommandDispatcher(innerDispatcherServices.BuildServiceProvider());
            });

        return collection;
    }
}