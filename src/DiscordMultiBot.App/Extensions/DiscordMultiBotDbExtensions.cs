using Microsoft.Extensions.Configuration;

namespace DiscordMultiBot.App.Extensions;

public static class DiscordMultiBotDbExtensions
{
    public static string? GetConnectionString(this IConfiguration configuration, string provider)
    {
        var basePath = configuration[$"ConnectionStrings:{provider}"];
        basePath = basePath?.Replace("{BasePath}\\", AppDomain.CurrentDomain.BaseDirectory);

        return basePath;
    }
}