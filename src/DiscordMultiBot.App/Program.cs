using System.Text;
using DiscordMultiBot.App;
using DiscordMultiBot.App.Extensions;
using DiscordMultiBot.PollService.Extensions;

var botBuilder = new DiscordMultiBotBuilder();

string? connString = botBuilder.Configuration.GetConnectionString("SQLite");
if (connString is null)
{
    throw new Exception();
}

botBuilder.Services
    .AddPollApiRepositories(connString, s => s.AddDiscordMultiBotCommands());

var bot = botBuilder.Build();
await bot.RunAsync();