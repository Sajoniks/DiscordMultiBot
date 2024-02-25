using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Models;
using DiscordMultiBot.App.Models.Audio;

namespace DiscordMultiBot.App.Modules.Audio;

[Group("music", "Music commands")]
public class AudioModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordGuildAudioManager _audioManager;

    public AudioModule(DiscordGuildAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    [ComponentInteraction("audioplayer-skip", ignoreGroupNames: true, TreatAsRegex = false)]
    public async Task AudioPlayerSkipAsync()
    {
        var manager = await _audioManager.GetGuildAudioManagerAsync(Context.Guild);
        var vc = manager.Player.ConnectedChannel as SocketVoiceChannel;
        if (vc is not null && vc.ConnectedUsers.Any(u => u.Id == Context.User.Id))
        {
            await manager.SkipCurrentAudioAsync();
            
            await EmbedXmlUtils
                .CreateResponseEmbed("Play audio",
                    $"{MentionUtils.MentionUser(Context.User.Id)} has skipped current music")
                .RespondFromXmlAsync(Context);
        }
    }

    [ComponentInteraction("audioplayer-play", ignoreGroupNames: true, TreatAsRegex = false)]
    public async Task AudioPlayerPlayAsync()
    {
        var manager = await _audioManager.GetGuildAudioManagerAsync(Context.Guild);
        var vc = manager.Player.ConnectedChannel as SocketVoiceChannel;
        if (vc is not null && vc.ConnectedUsers.Any(u => u.Id == Context.User.Id))
        {
            if (manager.Player.IsPlaying)
            {
                manager.Player.Pause();

                await EmbedXmlUtils
                    .CreateResponseEmbed("Play audio",
                        $"{MentionUtils.MentionUser(Context.User.Id)} has paused the music")
                    .RespondFromXmlAsync(Context);
            }
            else if (manager.Player.IsPaused)
            {
                manager.Player.Play(TimeSpan.Zero);
                
                await EmbedXmlUtils
                    .CreateResponseEmbed("Play audio",
                        $"{MentionUtils.MentionUser(Context.User.Id)} has resumed the music")
                    .RespondFromXmlAsync(Context);
            }
        }
    }
    
    [SlashCommand("play", "Play music from url, or file")]
    public async Task PlayAsync(
        [Summary("url", "Audio url")] string url
    )
    {
        IVoiceChannel? vc = Context.Guild.VoiceChannels
            .FirstOrDefault(x => x.ConnectedUsers.Any(u => u.Id == Context.User.Id));
        
        if (vc is null)
        {
            await EmbedXmlUtils
                .CreateErrorEmbed("Play", "You are not in a voice channel")
                .RespondFromXmlAsync(Context, ephemeral: true);
        }
        else
        {
            try
            {
                await RespondAsync("Starting...");

                var localManager = await _audioManager.GetGuildAudioManagerAsync(Context.Guild);
                var request = await localManager.AddPlayAudioRequestAsync(new VoiceChannelAudio(
                    TrackId: url,
                    User: Context.User,
                    Source: Context.Channel,
                    VoiceChannel: vc
                ));
                
                if (!localManager.IsEnqueued(request))
                {
                    await ModifyOriginalResponseAsync((props) =>
                    {
                        EmbedXmlDoc doc =
                            EmbedXmlUtils.CreateResponseEmbed("Play audio", $"Playing `{url}` in the channel {MentionUtils.MentionChannel(vc.Id)}");
                        props.Components = doc.Comps;
                        props.Content = doc.Text;
                        props.Embeds = doc.Embeds;
                    });
                }
                else
                {
                    await ModifyOriginalResponseAsync((props) =>
                    {
                        EmbedXmlDoc doc =
                            EmbedXmlUtils.CreateResponseEmbed("Play audio", $"Enqueued `{url}` in the channel {MentionUtils.MentionChannel(vc.Id)}");
                        props.Components = doc.Comps;
                        props.Content = doc.Text;
                        props.Embeds = doc.Embeds;
                    });
                }
            }
            catch (Exception e)
            {
                await ModifyOriginalResponseAsync((props) =>
                {
                    EmbedXmlDoc doc =
                        EmbedXmlUtils.CreateErrorEmbed("Play audio error", "Failed to play audio. Try again later.");
                    props.Components = doc.Comps;
                    props.Content = doc.Text;
                    props.Embeds = doc.Embeds;
                });
            }
        }
    }
}