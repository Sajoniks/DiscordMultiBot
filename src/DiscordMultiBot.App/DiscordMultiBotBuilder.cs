using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        return new DiscordMultiBot(Configuration.Build(), Services.BuildServiceProvider());
    }
    
    public IConfigurationManager Configuration { get; }
    public IServiceCollection Services { get; }
}