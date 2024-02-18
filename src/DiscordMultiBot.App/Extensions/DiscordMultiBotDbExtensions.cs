using Microsoft.Extensions.Configuration;

namespace DiscordMultiBot.App.Extensions;

public static class DiscordMultiBotDbExtensions
{
    public static string? GetConnectionString(this IConfiguration configuration, string provider)
    {
        var connString = configuration[$"ConnectionStrings:{provider}"];
        if (connString is null)
            throw new NullReferenceException($"Connection string for provider \"{provider}\" was not found");
        
        int basePathIdx = connString.IndexOf("{BasePath}", StringComparison.Ordinal);
        if (basePathIdx >= 0)
        {
            string basePath = Environment.GetEnvironmentVariable("SQLITE_PATH") ?? ".";
            
            int endPathIdx = connString.IndexOf(';', basePathIdx);
            string fileName = Path.GetFullPath( Path.Combine(connString.Substring(basePathIdx, endPathIdx - basePathIdx)
                .Replace("{BasePath}", basePath)
                .Split('\\', '/')) );

            connString = connString.Substring(0, basePathIdx) + fileName + connString.Substring(endPathIdx);
        }
        
        return connString;
    }
}