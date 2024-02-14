using Microsoft.Extensions.Configuration;

namespace DiscordMultiBot.App.Extensions;

public static class DiscordMultiBotDbExtensions
{
    public static string? GetConnectionString(this IConfiguration configuration, string provider)
    {
        return configuration[$"ConnectionStrings:{provider}"];
    }
}