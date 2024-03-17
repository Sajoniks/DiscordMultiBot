namespace DiscordMultiBot.App.Models.Audio2.Interface;

public interface IAudioSubsystem<TAudioSource> where TAudioSource : IAudioSource
{
    void Start();
    void Stop();
    
    TAudioSource Create();
}