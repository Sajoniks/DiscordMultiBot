namespace DiscordMultiBot.App.Models.Audio;

public interface IAudioManager<TModel, TRequest>
{
    public Task<Guid> AddPlayAudioRequestAsync(TRequest request);
    public Task CancelAllRequests();
    public TModel Player { get; }
}