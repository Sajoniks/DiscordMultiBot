namespace DiscordMultiBot.App.Models.Audio;

public interface IAudioManager<TRequest>
{
    public Task<long> AddPlayAudioRequestAsync(TRequest request);
    public Task CancelAllRequests();
}