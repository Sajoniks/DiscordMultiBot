using System.Reflection;

namespace DiscordMultiBot.App.EmbedLayouts;

public static class LayoutManager
{
    private static readonly string BaseNamespace;
    
    static LayoutManager()
    {
        BaseNamespace = typeof(LayoutManager).Namespace!;
    }

    public static StreamReader CreateViewReader(string name)
    {
        string filename;
        if (name.StartsWith(BaseNamespace) && name.EndsWith(".xml")) // Fully qualified?
        {
            filename = name;
        }
        else
        {
            if (name.EndsWith(".xml"))
            {
                filename = $"{BaseNamespace}.{name}";
            }
            else
            {
                filename = $"{BaseNamespace}.{name}.xml";
            }
        }
        
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(filename) ?? throw new NullReferenceException();
        return new StreamReader(stream);
    }
}