﻿using Discord;

namespace DiscordMultiBot.App.Models.Audio;

public record VoiceChannelAudio(
    string TrackId,
    string Title,
    string Artist,
    string ThumbnailUrl,
    IVoiceChannel VoiceChannel, 
    IUser User, 
    IMessageChannel Source, 
    bool AutoPlay = true, 
    bool Silent = false,
    bool HighPriority = false, 
    Action? CompletionCallback = null
);

public interface IGuildAudioManager<TManager> 
    where TManager : IAudioManager<VoiceChannelAudio>
{
    public Task<TManager> GetGuildAudioManagerAsync(IGuild guild);
}