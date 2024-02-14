// See https://aka.ms/new-console-template for more information

using DiscordMultiBot.App;
using DiscordMultiBot.App.Extensions;
using DiscordMultiBot.PollService.Extensions;

var botBuilder = new DiscordMultiBotBuilder();

string? connString = botBuilder.Configuration.GetConnectionString("SQLite");
if (connString is null)
{
    throw new Exception();
}

botBuilder.Services.AddPollApi(connString);

var bot = botBuilder.Build();
await bot.RunAsync();