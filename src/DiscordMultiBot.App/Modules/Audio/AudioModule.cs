using Discord;
using Discord.Interactions;
using DiscordMultiBot.App.EmbedLayouts;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Models.Audio;
using YoutubeSearchApi.Net.Services;

namespace DiscordMultiBot.App.Modules.Audio;

[Group("music", "Music commands")]
public class AudioModule : InteractionModuleBase<SocketInteractionContext>
{
    private static Dictionary<ulong, NotificationMessage> ActiveNotifications = new(); // @todo

    private class NotificationMessage : IDisposable
    {
        private CancellationTokenSource _cts;
        private CancellationTokenRegistration _registration;
        private IMessageChannel _channel;
        private IGuild _guild;
        private ulong _messageId;

        public NotificationMessage(IGuild guild, IMessageChannel channel, ulong messageId, int msDelay = 2000)
        {
            _guild = guild;
            _channel = channel;
            _messageId = messageId;
            _cts = new CancellationTokenSource();
            _cts.CancelAfter(msDelay);
            _registration = _cts.Token.Register(() =>
            {
                lock (ActiveNotifications)
                {
                    ActiveNotifications.Remove(_guild.Id);
                    Dispose();

                    _ = channel.DeleteMessageAsync(messageId);
                }
            });
        }

        public ulong MessageId => _messageId;
        
        public void Dispose()
        {
            _registration.Dispose();
            _cts.Dispose();
        }
    }
        
    private readonly DiscordGuildAudioManager _audioManager;
    
    public AudioModule(DiscordGuildAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    [ComponentInteraction("audioplayer-skip", ignoreGroupNames: true, TreatAsRegex = false)]
    public async Task AudioPlayerSkipAsync()
    {
        var manager = await _audioManager.GetGuildAudioManagerAsync(Context.Guild);
        var vc = manager.VoiceChannel;
        
        if (vc?.ConnectedUsers.Any(u => u.Id == Context.User.Id) ?? false)
        {
            await manager.SkipCurrentAudioAsync();

            EmbedXmlDoc doc = EmbedXmlUtils.CreateResponseEmbed("Play audio",$"{MentionUtils.MentionUser(Context.User.Id)} has skipped current music");

            ulong messageId = 0;
            lock (ActiveNotifications)
            {
                if (ActiveNotifications.TryGetValue(Context.Guild.Id, out var msg))
                {
                    ulong guildId = Context.Guild.Id;
                    var channel = Context.Channel;
                    messageId = msg.MessageId;
                    
                    ActiveNotifications.Remove(Context.Guild.Id);
                    msg.Dispose();
                }
            }

            if (messageId != 0)
            {
                try
                {
                    await doc.ModifyMessageFromXmlAsync(messageId, Context.Channel);
                    lock (ActiveNotifications)
                    {
                        ActiveNotifications.Add(Context.Guild.Id, new NotificationMessage(Context.Guild, Context.Channel, messageId));
                    }
                }
                catch (Exception)
                {
                    messageId = 0;
                }
            }

            if (messageId == 0)
            {
                await doc.RespondFromXmlAsync(Context);
                var newMessage = await GetOriginalResponseAsync();
                lock (ActiveNotifications)
                {
                    ActiveNotifications.Add(Context.Guild.Id, new NotificationMessage(Context.Guild, Context.Channel, newMessage.Id));
                }
            }
        }
    }

    [ComponentInteraction("audioplayer-play", ignoreGroupNames: true, TreatAsRegex = false)]
    public async Task AudioPlayerPlayAsync()
    {
      
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

                if (!localManager.TryFindUri(url, out var _))
                {
                    VoiceChannelAudio? newAudio = null;
                    using (var httpClient = new HttpClient())
                    {
                        if (url.StartsWith("https://"))
                        {
                            // @todo
                            // need to parse metadata

                            newAudio = new VoiceChannelAudio(
                                TrackId: url,
                                User: Context.User,
                                Title: "",
                                Artist: "",
                                ThumbnailUrl: "",
                                Source: Context.Channel,
                                VoiceChannel: vc
                            );
                        }
                        else
                        {
                            var querier = new YoutubeSearchClient(httpClient);
                            var results = await querier.SearchAsync(url);
                            
                            if (results.Results.Any())
                            {
                                var result = results.Results.First();
                                newAudio = new VoiceChannelAudio(
                                    TrackId: result.Url,
                                    User: Context.User,
                                    Title: result.Title,
                                    Artist: result.Author,
                                    ThumbnailUrl: result.ThumbnailUrl,
                                    Source: Context.Channel,
                                    VoiceChannel: vc);
                            }
                        }
                    }

                    if (newAudio is null)
                    {
                        await EmbedXmlCreator.CreateEmbed(Layouts.AudioSearchError, new Dictionary<string, string>
                        {
                            { "Query", url }
                        }).ModifyOriginalResponseFromXmlAsync(Context);
                    }
                    else
                    {
                        var request = await localManager.AddPlayAudioRequestAsync(newAudio);
                        
                        await EmbedXmlCreator.CreateEmbed(Layouts.PlayAudio, new Dictionary<string, string>
                        {
                            { "CurrentSong", newAudio.Title },
                            { "Username", MentionUtils.MentionUser(Context.User.Id) },
                            { "Channel", MentionUtils.MentionChannel(newAudio.VoiceChannel.Id) }
                        }).ModifyOriginalResponseFromXmlAsync(Context);
                    }
                }
                else
                {
                    var tags = localManager.DescribeUrl(url);
                    var request = await localManager.AddPlayAudioRequestAsync(new VoiceChannelAudio(
                        TrackId: url,
                        User: Context.User,
                        Title: tags.Title,
                        Artist: tags.Artist,
                        ThumbnailUrl: "",
                        Source: Context.Channel,
                        VoiceChannel: vc
                    ));

                    await EmbedXmlCreator.CreateEmbed(Layouts.PlayAudio, new Dictionary<string, string>
                    {
                        { "CurrentSong", url },
                        { "Username", MentionUtils.MentionUser(Context.User.Id) },
                        { "Channel", MentionUtils.MentionChannel(vc.Id) }
                    }).ModifyOriginalResponseFromXmlAsync(Context);
                }
            }
            catch (Exception e)
            {
                await EmbedXmlUtils
                    .CreateErrorEmbed("Play audio error", "Failed to play audio. Try again later.")
                    .ModifyOriginalResponseFromXmlAsync(Context);
                
                Console.WriteLine(e);
            }
        }
    }
}