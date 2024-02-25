namespace DiscordMultiBot.App.Models.Audio;

public record AudioParameters(Uri Uri, float Volume = 1.0f, bool Looping = false);

public interface IAudioPlayerModel : IDisposable
{
    public void Open(AudioParameters parameters);
    public void Play(TimeSpan start);
    public void Pause();
    public void Stop();
    public void Forward(TimeSpan forwardAmount);
    public int Id { get; }
}