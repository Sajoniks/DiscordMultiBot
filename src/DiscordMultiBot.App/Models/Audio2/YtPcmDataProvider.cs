using DiscordMultiBot.App.Models.Audio2.Interface;

namespace DiscordMultiBot.App.Models.Audio2;

public class YtPcmDataProvider : IPcmDataProvider
{
    public void Dispose()
    {
        // TODO release managed resources here
    }

    public YtPcmDataProvider(Uri uri)
    {
        
    }

    public int Stream(int size)
    {
        throw new NotImplementedException();
    }

    public bool EndOfStream { get; }
    public byte[] Buffer { get; }
    public bool Looping { get; }
    public int BufferedSize { get; }
}