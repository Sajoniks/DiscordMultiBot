using System.Collections.Concurrent;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LogMessage = Discord.LogMessage;
using BotLogMessage = DiscordMultiBot.App.Logging.LogMessage;
using LogSeverity = DiscordMultiBot.App.Logging.LogSeverity;

namespace DiscordMultiBot.App;

public sealed class DiscordMultiBot
{
    private InteractionService _interactionService;
    private DiscordSocketClient _bot;
    private bool _maintenanceMode;

    private readonly LogManager _logManager;
    private static DiscordMultiBot? _botApp;
    
    public static DiscordMultiBot Instance
    {
        get
        {
            if (_botApp is null)
            {
                throw new NullReferenceException();
            }

            return _botApp;
        }
    }

    public DiscordMultiBot(IConfiguration configuration, IServiceProvider services)
    {
        Configuration = configuration;
        Services = services;
        _bot = services.GetRequiredService<DiscordSocketClient>();
        _interactionService = new InteractionService(_bot);
        _maintenanceMode = false;
        {
            string maintenanceEnv = Environment.GetEnvironmentVariable("BOT_MAINTENANCE") ?? "";
            if (maintenanceEnv.Length != 0)
            {
                _maintenanceMode = (maintenanceEnv.Equals("1") ||
                                    maintenanceEnv.Equals("true", StringComparison.InvariantCultureIgnoreCase));
            }
        }
        
        _bot.InteractionCreated += BotOnInteractionCreated;
        _bot.Ready += BotOnReady;
        _bot.MessageReceived += BotOnMessageReceived;
        _bot.Log += BotOnLog;
        _interactionService.Log += InteractionServiceOnLog;
        _botApp = this;

        {
            string config = configuration["Logger:MinLevel"] ?? "";
            if (!Enum.TryParse(config, out LogSeverity severity))
            {
                severity = LogSeverity.Info;
            }

            _logManager = new LogManager(severity);
            _logManager.Message += LogManagerOnMessage;
        }
    }

    public Logger CreateLogger(string name)
    {
        return _logManager.CreateLogger(name);
    }

    private Task LogManagerOnMessage(BotLogMessage arg)
    {
        Console.WriteLine(arg);
        return Task.CompletedTask;
    }

    private Task BotOnLog(LogMessage arg)
    {
        Console.WriteLine(arg);
        return Task.CompletedTask;
    }
    
    private Task InteractionServiceOnLog(LogMessage arg)
    {
        Console.WriteLine(arg);
        return Task.CompletedTask;
    }

    public IConfiguration Configuration { get; }
    public IServiceProvider Services { get; }

    public async Task RunAsync()
    {
        if (_maintenanceMode)
        {
            Console.WriteLine("*** STARTING BOT IN MAINTANCE MODE ***");
        }
        
        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), Services);
        await _bot.LoginAsync(TokenType.Bot, Configuration["Discord:Bot:Token"]);
        await _bot.StartAsync();
        
        await Task.Delay(-1);
    }

    private async Task BotOnInteractionCreated(SocketInteraction arg)
    {
        var ctx = new SocketInteractionContext(_bot, arg);
        
        if (_maintenanceMode)
        {
            if (UInt64.TryParse(Configuration["Discord:Bot:AdminId"], out var id));
            {
                if (id != arg.User.Id)
                {
                    await EmbedXmlUtils
                        .CreateErrorEmbed("Command not processed", "Bot is running maintenance. Please try again later.")
                        .RespondFromXmlAsync(ctx, ephemeral: true);
                    
                    return;
                }
            }
        }
        
        await _interactionService.ExecuteCommandAsync(ctx, Services);
    }

    private Task BotOnMessageReceived(SocketMessage arg)
    {
        return Task.CompletedTask;
    }

    private async Task BotOnReady()
    {
        await _interactionService.RegisterCommandsGloballyAsync();
    }
}