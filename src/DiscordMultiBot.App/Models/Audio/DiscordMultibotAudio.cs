using Discord;
using Discord.WebSocket;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Logging;
using DiscordMultiBot.App.Models.Audio2;
using Microsoft.Extensions.Configuration;

namespace DiscordMultiBot.App.Models.Audio;

public record AudioFinishedEventArgs(Exception? Exception, VoiceChannelAudio Audio);

public class DiscordAudioManager : IAudioManager<VoiceChannelAudio>
{
    private record VoiceChannelAudioRequest(long Id, VoiceChannelAudio Audio);

    private object _lock = new object();
    
    private Queue<VoiceChannelAudioRequest> _requestQueue = new();
    private DiscordAudioSubsystem? _ss;
    private VoiceChannelAudio? _activeRequest = null;
    private DiscordAudioSource? _activeAudioSource = null;
    private IConfiguration _configuration;
    private Logger _logger;
    private ulong _audioPlayerMessageId = 0;
    private ulong _lastNotificationMessageId = 0;
    private IGuild _ownerGuild;

    public bool TryFindUri(string trackId, out Uri? uri)
    {
        uri = null;
        try
        {
            string sectionPath = "Bot:Audio:Files";
            IConfigurationSection files = _configuration.GetSection(sectionPath);

            var first = int.Parse( files.GetChildren().First().Key );
            var last = int.Parse( files.GetChildren().Last().Key );

            IConfigurationSection? target = null;
            for (int i = first; i <= last; ++i)
            {
                IConfigurationSection fileSection = files.GetSection(i.ToString());
                string? fileName = fileSection["Name"];
                
                if (fileName?.Equals(trackId) ?? false)
                {
                    target = fileSection;
                    break;
                }
            }

            IConfigurationSection? file = null;
            if (target is not null)
            {
                string targetSection = "Files";
                IConfigurationSection targetFiles = target.GetSection(targetSection);

                var targetFirst = int.Parse( targetFiles.GetChildren().First().Key );
                var targetLast = int.Parse( targetFiles.GetChildren().Last().Key );
                var rnd = Random.Shared.Next(targetFirst, targetLast + 1);

                file = targetFiles.GetSection(rnd.ToString());
            }

            if (file is not null)
            {
                uri = new Uri(file["Path"]!);
            }
        }
        catch (Exception)
        {
            // ignore
        }

        return (uri is not null);
    }
    
    private StreamingResource CreateStreamingResourceByTrackId(string trackId)
    {
        if (trackId.Length == 0)
        {
            throw new ArgumentException(nameof(trackId));
        }

        Uri? uri = null;
        bool looping = false;
        float volume = 1.0f;
        try
        {
            string sectionPath = "Bot:Audio:Files";
            IConfigurationSection files = _configuration.GetSection(sectionPath);

            var first = int.Parse( files.GetChildren().First().Key );
            var last = int.Parse( files.GetChildren().Last().Key );

            IConfigurationSection? defaultSection = null;
            IConfigurationSection? target = null;
            for (int i = first; i <= last; ++i)
            {
                IConfigurationSection fileSection = files.GetSection(i.ToString());
                string? fileName = fileSection["Name"];
                
                if (fileName?.Equals(trackId) ?? false)
                {
                    target = fileSection;
                    break;
                }
                if (fileName?.Equals("default") ?? false)
                {
                    defaultSection = fileSection;
                }
            }

            if (target is null)
            {
                target = defaultSection;
            }

            IConfigurationSection? file = null;
            IConfigurationSection? fileProps = null;
            if (target is not null)
            {
                string targetSection = "Files";
                IConfigurationSection targetFiles = target.GetSection(targetSection);

                var targetFirst = int.Parse( targetFiles.GetChildren().First().Key );
                var targetLast = int.Parse( targetFiles.GetChildren().Last().Key );
                var rnd = Random.Shared.Next(targetFirst, targetLast + 1);

                file = targetFiles.GetSection(rnd.ToString());
                fileProps = target.GetSection("Properties");
            }

            if (file is not null)
            {
                IConfigurationSection localProps = file.GetSection("Properties");
                if (localProps["volume"] is null)
                {
                    if (fileProps is not null && fileProps["volume"] is not null)
                    {
                        if (!float.TryParse(fileProps["volume"], out volume))
                        {
                            volume = 1.0f;
                        }
                    }
                }
                else
                {
                    if (!float.TryParse(localProps["volume"], out volume))
                    {
                        volume = 1.0f;
                    }
                }
                
                if (localProps["looping"] is null)
                {
                    if (fileProps is not null && fileProps["looping"] is not null)
                    {
                        if (!bool.TryParse(fileProps["looping"], out looping))
                        {
                            looping = false;
                        }
                    }
                }
                else
                {
                    if (!bool.TryParse(localProps["looping"], out looping))
                    {
                        looping = false;
                    }
                }
                uri = new Uri(file["Path"]!);
            }
        }
        catch (Exception)
        {
            // ignore
        }

        if (uri is null)
        {
            _logger.Error($"Tried to request Track \"{trackId}\", but it does not exist");
            
            throw new FileNotFoundException();
        }

        if (uri.IsFile)
        {
            string filePath = System.Web.HttpUtility.UrlDecode(uri.AbsolutePath);
            if (!File.Exists(filePath))
            {
                _logger.Error($"Tried to request file Track \"{trackId}\" at path that does not exist \"{filePath}\"");
                
                throw new FileNotFoundException(filePath);
            }
        }

        return new StreamingResource(Source: uri, Looping: looping, Volume: volume);
    }
    
    public DiscordAudioManager(IGuild ownerGuild, IConfiguration configuration)
    {
        _ownerGuild = ownerGuild;
        _configuration = configuration;
        _logger = DiscordMultiBot.Instance.CreateLogger("Audio Manager");
    }

    private void ProcessNextAudioRequest()
    {
        if (_ss is null)
        {
            throw new InvalidOperationException("Subsystem was not created");
        }
        
        _logger.Info("Next audio is being processed");
        
        VoiceChannelAudioRequest next = _requestQueue.Dequeue();
        _activeRequest = next.Audio;

        string title = "";
        string artist = "";
        var audio = _ss.Create();
        if (next.Audio.TrackId.StartsWith("https://"))
        {
            audio.DataProvider = new YtPcmDataProvider(new Uri(next.Audio.TrackId));
        }
        else
        {
            var streamingResource = CreateStreamingResourceByTrackId(next.Audio.TrackId);
            audio.DataProvider = new FFmpegPcmDataProvider(streamingResource);
            TagLib.File tags = TagLib.File.Create(System.Web.HttpUtility.UrlDecode(streamingResource.Source.AbsolutePath));
            title = tags.Tag.Title ?? "Unknown Title";
            artist = tags.Tag.JoinedAlbumArtists ?? "Unknown Artist";
        }
        audio.Play();
        _activeAudioSource = audio;
        
        if (!next.Audio.Silent)
        {
            var creator = new EmbedXmlCreator();
            creator.Bindings["CurrentSong"] = title;
            creator.Bindings["Artist"] = artist;

            if (_audioPlayerMessageId == 0)
            {
                _ = creator
                    .Create("AudioPlayer")
                    .SendMessageFromXmlAsync(next.Audio.Source)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            _audioPlayerMessageId = t.Result.Id;
                        }
                    });
            }
            else
            {
                _ = creator
                    .Create("AudioPlayer")
                    .ModifyMessageFromXmlAsync(_audioPlayerMessageId, next.Audio.Source);
            }
        }
    }

    public Task SkipCurrentAudioAsync()
    {
        lock (_lock)
        {
            _activeRequest = null;
        }

        _activeAudioSource?.Stop();
        _activeAudioSource = null;
        
        return Task.CompletedTask;
    }
    
    public async Task<long> AddPlayAudioRequestAsync(VoiceChannelAudio request)
    {
        if (_ss is null)
        {
            _ss = await Task.Run(() => DiscordAudioSubsystem.CreateSubsystemAsync(request.VoiceChannel));
            _ss.AudioStopped += OnAudioStopped;
            _ss.Stopped += OnSubsystemStopped;
            _ss.Started += OnSubsystemStarted;
            _ss.Start();
        }
        
        _logger.Info($"Added new audio request [Id = {request.TrackId}  HighPriority = {request.HighPriority}  Num Requests = {_requestQueue.Count + 1}]");
        
        if (request.HighPriority)
        {
            await CancelAllRequests();
        }
        
        var r = new VoiceChannelAudioRequest(
            Id: Environment.TickCount64,
            Audio: request
        );
        _requestQueue.Enqueue(r);

        if (_activeRequest is null)
        {
            // first audio in the queue
            ProcessNextAudioRequest();
        }

        return r.Id;
    }

    private void OnSubsystemStarted()
    {
        
    }

    private void OnSubsystemStopped()
    {
        _ss = null;
        _activeAudioSource = null;
        _activeRequest = null;
        _requestQueue.Clear();
    }

    private void OnAudioStopped(DiscordAudioSource obj)
    {
        if (obj == _activeAudioSource)
        {
            _activeAudioSource = null;
        }

        _activeRequest = null;
        
        if (_requestQueue.Any())
        {
            ProcessNextAudioRequest();
        }
    }

    public Task CancelAllRequests()
    {
        _requestQueue.Clear();
        if (_activeRequest is not null)
        {
            _activeRequest = null;
        }

        _activeAudioSource = null;
        if (_ss is not null)
        {
            return _ss.StopAllAsync();
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    public bool IsEnqueued(long id)
    {
        return _requestQueue.Any(x => x.Id == id);
    }
    
    public SocketVoiceChannel? VoiceChannel => _activeRequest?.VoiceChannel as SocketVoiceChannel;
}

public class DiscordGuildAudioManager : IGuildAudioManager<DiscordAudioManager>
{
    private Dictionary<ulong, DiscordAudioManager> _guilds = new();
    
    public Task<DiscordAudioManager> GetGuildAudioManagerAsync(IGuild guild)
    {
        lock (_guilds)
        {
            if (!_guilds.TryGetValue(guild.Id, out var manager))
            {
                manager = new DiscordAudioManager(guild, DiscordMultiBot.Instance.Configuration);
                _guilds.Add(guild.Id, manager);
            }

            return Task.FromResult(manager);
        }
    }
}