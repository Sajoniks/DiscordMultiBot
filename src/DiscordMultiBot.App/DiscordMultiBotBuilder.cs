using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMultiBot.App.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiscordMultiBot.App;

public sealed class DiscordMultiBotBuilder
{
    public DiscordMultiBotBuilder()
    {
        Configuration = new ConfigurationManager();
        Services = new ServiceCollection();

        bool isDevEnv = false;
        string? env = Environment.GetEnvironmentVariable("BOT_ENV");
        if (env is not null)
        {
            string currPath = AppDomain.CurrentDomain.BaseDirectory;
            string jsonPath = "";

            if (env.Equals("DEV"))
            {
                isDevEnv = true;
                jsonPath = "appsettings.Dev.json";
            }
            else if (env.Equals("PROD"))
            {
                isDevEnv = false;
                jsonPath = "appsettings.json";
            }

            if (jsonPath.Length > 0 )
            {
                string fullPath = Path.GetFullPath(jsonPath, currPath);
                if (File.Exists(fullPath))
                {
                    Configuration.AddJsonFile(jsonPath);
                }
            }
            
            string pollVotesConfigName = "pollVoteSettings.json";
            string pollVotesConfigPath = Path.GetFullPath(Path.Combine("Configuration", pollVotesConfigName), currPath);
            Configuration.AddJsonFile(pollVotesConfigPath);

            string audioConfigName = "audiosettings.json";
            string audioConfigPath = Path.GetFullPath(Path.Combine("Configuration", audioConfigName), currPath);
            Configuration.AddJsonFile(audioConfigPath);
        }

        string? maintance = Environment.GetEnvironmentVariable("BOT_MAINTANCE");
        if (maintance is not null)
        {
            // @todo
        }

        
        var discordBotConfig = new DiscordSocketConfig
        {
            LogLevel = isDevEnv ? LogSeverity.Verbose : LogSeverity.Error,
            AlwaysResolveStickers = false,
        };

        Services
            .AddSingleton(discordBotConfig)
            .AddSingleton<DiscordSocketClient>();
    }

    public DiscordMultiBot Build()
    {
        Services
            .AddSingleton(_ =>
            {
                var botCommands = new ServiceCollection();

                botCommands
                    .Add(Services)
                    //--------------------------------------------------
                    // Command handlers
                    //
                    .AddTransient<ISocketBotCommandHandler<CompletePollBotCommand>, CompletePollBotCommandHandler>()
                    .AddTransient<ISocketBotCommandHandler<WritePollResultsBotCommand>, WritePollResultsCommandHandler>()
                    .AddTransient<ISocketBotCommandHandler<UpdatePollMessageBotCommand>, UpdatePollMessageCommandHandler>()
                    .AddTransient<ISocketBotCommandHandler<MakePollVoteBotCommand>, MakePollVoteCommandHandler>()
                    .AddTransient<ISocketBotCommandHandler<ClearPollBotCommand>, ClearPollCommandHandler>()
                    .AddTransient<ISocketBotCommandHandler<CreatePollBotCommand>, CreatePollBotCommandHandler>();

                return new BotCommandDispatcher(botCommands.BuildServiceProvider());
            });
        return new DiscordMultiBot(Configuration.Build(), Services.BuildServiceProvider());
    }
    
    public IConfigurationManager Configuration { get; }
    public IServiceCollection Services { get; }
}