using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMultiBot.App.Modules.Audio;
using DiscordMultiBot.App.Modules.Misc;
using DiscordMultiBot.App.Modules.Poll;
using DiscordMultiBot.App.Modules.Voting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.App;

public sealed class DiscordMultiBot
{
    private InteractionService _interactionService;
    private DiscordSocketClient _bot;
    
    public DiscordMultiBot(IConfiguration configuration, IServiceProvider services)
    {
        Configuration = configuration;
        Services = services;
        _bot = services.GetRequiredService<DiscordSocketClient>();
        _interactionService = new InteractionService(_bot);
        
        _bot.InteractionCreated += BotOnInteractionCreated;
        _bot.Ready += BotOnReady;
        _bot.MessageReceived += BotOnMessageReceived;
        _bot.Log += BotOnLog;
        _interactionService.Log += InteractionServiceOnLog;
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
        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), Services);
        await _bot.LoginAsync(TokenType.Bot, Configuration["Discord:Bot:Token"]);
        await _bot.StartAsync();
        
        await Task.Delay(-1);
    }

    private async Task BotOnInteractionCreated(SocketInteraction arg)
    {
        var ctx = new SocketInteractionContext(_bot, arg);
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