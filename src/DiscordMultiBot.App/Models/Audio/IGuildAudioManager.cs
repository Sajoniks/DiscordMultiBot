using Discord;

namespace DiscordMultiBot.App.Models.Audio;

public record VoiceChannelAudio(
    string TrackId, 
    IVoiceChannel VoiceChannel, 
    IUser User, 
    IMessageChannel Source, 
    bool AutoPlay = true, 
    bool Silent = false,
    bool HighPriority = false, 
    Action? CompletionCallback = null
);

public interface IGuildAudioManager<TModel, TManager> 
    where TModel : IAudioPlayerModel
    where TManager : IAudioManager<TModel, VoiceChannelAudio>
{
    public Task<TManager> GetGuildAudioManagerAsync(IGuild guild);
}