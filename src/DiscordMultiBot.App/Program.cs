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

Console.WriteLine("AUDIOS_PATH: {0}", arg: Environment.GetEnvironmentVariable("AUDIOS_PATH"));
Console.WriteLine("SQLITE_PATH: {0}", arg: Environment.GetEnvironmentVariable("SQLITE_PATH"));
Console.WriteLine("BOT_ENV: {0}", arg: Environment.GetEnvironmentVariable("BOT_ENV"));
Console.WriteLine("CONN_STR: {0}", arg: connString);

botBuilder.Services
    .AddPollApiRepositories(connString, s => s.AddDiscordMultiBotCommands());

var bot = botBuilder.Build();
await bot.RunAsync();