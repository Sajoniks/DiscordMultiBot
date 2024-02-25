using Discord;
using DiscordMultiBot.App.EmbedXml;
using DiscordMultiBot.App.Logging;
using Microsoft.Extensions.Configuration;

namespace DiscordMultiBot.App.Models.Audio;

public record AudioFinishedEventArgs(Exception? Exception, VoiceChannelAudio Audio);

public class DiscordAudioManager : IAudioManager<DiscordAudioPlayer, VoiceChannelAudio>
{
    private record VoiceChannelAudioRequest(Guid Guid, VoiceChannelAudio Audio);
    private Queue<VoiceChannelAudioRequest> _requestQueue = new();
    private VoiceChannelAudio? _currentAudio = null;
    private DiscordAudioPlayer _player = new();
    private IConfiguration _configuration;
    private Logger _logger;
    private ulong _audioPlayerMessageId = 0;
    private IGuild _ownerGuild;
    
    private AudioParameters FindAudioParametersByTrackId(string trackId)
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

        return new AudioParameters(uri, volume, looping);
    }
    
    public DiscordAudioManager(IGuild ownerGuild, IConfiguration configuration)
    {
        _ownerGuild = ownerGuild;
        _configuration = configuration;
        _logger = DiscordMultiBot.Instance.CreateLogger("Audio Manager");
        _player.Disconnected += PlayerDisconnected;
        _player.PlaybackFinished += PlayerPlaybackFinished;
    }

    private void PlayerDisconnected(Exception? obj)
    {
        _requestQueue.Clear();
        _player.Dispose();
        _player = new DiscordAudioPlayer();
    }

    private void PlayerOnVoiceChannelEmpty(object? sender, EventArgs args)
    {
        if (_currentAudio is not null)
        {
            if (!_currentAudio.Silent)
            {
                _currentAudio.Source.SendMessageAsync("Stopping player. Everyone has left the channel.");
            }

            _currentAudio = null;
        }

        _requestQueue.Clear();
        _player.Dispose();
        _player = new DiscordAudioPlayer();
    }

    private void PlayerPlaybackFinished(Exception? e)
    {
        if (_currentAudio is not null)
        {
            _currentAudio = null;
        }

        if (_requestQueue.Count != 0)
        {
            ProcessNextAudioRequest();
        }
    }

    private void ProcessNextAudioRequest()
    {
        _logger.Info("Next audio is being processed");
        
        VoiceChannelAudioRequest next = _requestQueue.Dequeue();
        _currentAudio = next.Audio;
        var nextParms = FindAudioParametersByTrackId(next.Audio.TrackId);
        _player.Open(nextParms);
        if (next.Audio.AutoPlay)
        {
            _player.Play(TimeSpan.Zero);
        }

        if (!next.Audio.Silent)
        {
            var tags = TagLib.File.Create(System.Web.HttpUtility.UrlDecode(nextParms.Uri.AbsolutePath));

            var creator = new EmbedXmlCreator();
            creator.Bindings["CurrentSong"] = tags.Tag.Title ?? "Unknown Title";
            creator.Bindings["Artist"] = tags.Tag.JoinedAlbumArtists ?? "Unknown Artist";

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

        _player.AttachToChannelAsync(next.Audio.VoiceChannel);
    }

    public Task SkipCurrentAudioAsync()
    {
        if (_currentAudio is not null)
        {
            _currentAudio = null;
            _player.Stop();
        }

        if (_requestQueue.Any())
        {
            ProcessNextAudioRequest();
        }
        
        return Task.CompletedTask;
    }
    
    public Task<Guid> AddPlayAudioRequestAsync(VoiceChannelAudio request)
    {
        _logger.Info($"Added new audio request [Id = {request.TrackId}  HighPriority = {request.HighPriority}  Num Requests = {_requestQueue.Count + 1}]");
        
        if (request.HighPriority)
        {
            _requestQueue.Clear();
            if (_currentAudio is not null)
            {
                _currentAudio = null;
                _player.Stop();
            }
        }
        
        var r = new VoiceChannelAudioRequest(
            Guid: Guid.NewGuid(),
            Audio: request
        );
        _requestQueue.Enqueue(r);

        if (_currentAudio is null)
        {
            // first audio in the queue
            ProcessNextAudioRequest();
        }
        
        return Task.FromResult(r.Guid);
    }

    public Task CancelAllRequests()
    {
        return Task.CompletedTask;
    }

    public bool IsEnqueued(Guid guid)
    {
        return _requestQueue.Any(x => x.Guid.Equals(guid));
    }

    public DiscordAudioPlayer Player => _player;
}

public class DiscordGuildAudioManager : IGuildAudioManager<DiscordAudioPlayer, DiscordAudioManager>
{
    private Dictionary<ulong, DiscordAudioManager> _guilds = new();
    
    public Task<DiscordAudioManager> GetGuildAudioManagerAsync(IGuild guild)
    {
        if (!_guilds.TryGetValue(guild.Id, out var manager))
        {
            manager = new DiscordAudioManager(guild, DiscordMultiBot.Instance.Configuration);
            _guilds.Add(guild.Id, manager);
        }

        return Task.FromResult(manager);
    }
}