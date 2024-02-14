using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMultiBot.App;

public sealed class DiscordMultiBot
{
    public DiscordMultiBot(IConfiguration configuration, IServiceProvider services)
    {
        Configuration = configuration;
        Services = services;
    }
    
    public IConfiguration Configuration { get; }
    public IServiceProvider Services { get; }

    public async Task RunAsync()
    {
        var bot = Services.GetRequiredService<DiscordSocketClient>();

        await bot.LoginAsync(TokenType.Bot, Configuration["Discord:Bot:Token"]);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}