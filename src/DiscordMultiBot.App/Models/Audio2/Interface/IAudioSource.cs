namespace DiscordMultiBot.App.Models.Audio2.Interface;

public interface IAudioSource : IDisposable
{
    IPcmDataProvider DataProvider { set; }
    
    bool Playing { get; }
    bool Looping { get; }

    void Play();
    void Forward(TimeSpan amount);
    void Stop();
}