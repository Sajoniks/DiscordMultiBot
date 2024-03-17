namespace DiscordMultiBot.App.Models.Audio2.Interface;

public interface IPcmDataProvider : IDisposable
{
    int Stream(int size);
    
    bool EndOfStream { get; }
    byte[] Buffer { get; }
    bool Looping { get; }
    int BufferedSize { get; }
}