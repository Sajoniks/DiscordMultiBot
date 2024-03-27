using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordMultiBot.App.Models.Audio2.Interface;

namespace DiscordMultiBot.App.Models.Audio2;

public class DiscordAudioSource : IAudioSource
{
    public IPcmDataProvider DataProvider
    {
        set => _dataProvider = value;
    }
    
    public bool Playing => _playing;
    public bool Looping => _dataProvider?.Looping ?? false;
    
    public DiscordAudioSource(DiscordAudioSubsystem subsystem, Stream outputStream)
    {
        _ss = subsystem;
        _outputStream = outputStream;
        _disposed = false;
        _stopped = 0;
        _playing = false;
    }
    
    public void Play()
    {
        if (_dataProvider is null) throw new NullReferenceException();
        if (_playing) return;

        _playing = true;
        _ss.AddAudioSource(this);
    }

    public void Forward(TimeSpan amount)
    {
        // not supported
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
        {
            Console.WriteLine("Stop requested");
            _playing = false;
            Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("Flushing contents");
                    _outputStream.Flush();
                    _outputStream.Dispose();
                }
                catch (Exception)
                { /**/ }
                finally
                {
                    _stopped = 1;
                    Console.WriteLine("Stopped");
                }
            });
        }
    }

    public bool StopRequested => _stopRequested == 1;
    public bool Closed => _stopped == 1;

    public int Update()
    {
        if (_dataProvider is null) throw new NullReferenceException();
        if (StopRequested || Closed)
        {
            return 0;
        }
        
        if (_dataProvider.EndOfStream)
        {
            return 0;
        }

        int streamed = _dataProvider.Stream(256);
        if (streamed > 0)
        {
            try
            {
                _outputStream.Write(_dataProvider.Buffer, 0, _dataProvider.BufferedSize);
            }
            catch (Exception)
            { /**/ }
        }

        return streamed;
    }

    public void Dispose()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DiscordAudioSource));
        _disposed = true;
        _dataProvider?.Dispose();
    }

    private bool _disposed;
    private Stream _outputStream;
    private IPcmDataProvider? _dataProvider;
    private DiscordAudioSubsystem _ss;
    private bool _playing;
    private int _stopped;
    private int _stopRequested;
}

public class DiscordAudioSubsystem : IAudioSubsystem<DiscordAudioSource>
{
    public DiscordAudioSubsystem(IGuild guid, IVoiceChannel vc, IAudioClient client)
    {
        _guild = guid;
        _client = client;
        _vc = vc;
        _client.Disconnected += ClientOnDisconnected;
        _client.ClientDisconnected += ClientOnClientDisconnected;
        
        _audios = new List<DiscordAudioSource>();
        _processAudioEvent = new AutoResetEvent(false);
    }

    private Task ClientOnClientDisconnected(ulong arg)
    {
        if (_workerThread is not null && _pendingExit != 1)
        {
            var users = (_vc as SocketVoiceChannel)?.ConnectedUsers.Count(u => !u.IsBot) ?? 0;
            if (users == 0)
            {
                Stop();
                _client.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    private Task ClientOnDisconnected(Exception arg)
    {
        if (_workerThread is not null && _pendingExit != 1)
        {
            Stop();
        }

        return Task.CompletedTask;
    }

    private void WorkerThread()
    {
        Started?.Invoke();
        
        do
        {
            bool sleep = false;
            lock (_audios)
            {
                if (_audios.Count == 0)
                {
                    sleep = true;
                }
                else
                {
                    _updateIndex = (_updateIndex + 1) % _audios.Count;
                }
            }

            if (sleep)
            {
                Console.WriteLine("AudioSubsystem went sleeping (no audios to process)");
                // sleep until 
                _processAudioEvent.WaitOne();
                
                Console.WriteLine("AudioSubsystem woke up");

                continue;
            }

            var src = _audios[_updateIndex];
            if (src.StopRequested)
            {
                if (src.Closed)
                {
                    Console.WriteLine("Audio closed");
                    
                    DeleteAudioSource(src);
                    AudioStopped?.Invoke(src);
                }
            }
            else
            {
                int upd = src.Update();

                if (upd == 0)
                {
                    // stop
                    src.Stop();
                }
            }
            

        } while (_pendingExit == 0);

        foreach (var audio in _audios)
        {
            audio.Dispose();
        }
        _audios.Clear();
        
        Stopped?.Invoke();
    }

    public event Action<DiscordAudioSource>? AudioStopped;
    public event Action? Started;
    public event Action? Stopped;
    
    public void Start()
    {
        if (_workerThread is not null) throw new InvalidOperationException();

        Console.WriteLine("Starting subsystem thread");
        
        _workerThread = new Thread(WorkerThread);
        _workerThread.Name = "Discord Audio Worker Thread";
        _workerThread.Start();
    }

    public void Stop()
    {
        if (_workerThread is null) throw new InvalidOperationException();

        if (Interlocked.Exchange(ref _pendingExit, 1) == 0)
        {
            if (_workerThread != Thread.CurrentThread)
            {
                Console.WriteLine("Stopping subsystem thread");
                
                _processAudioEvent.Set();
                _workerThread.Join();
            }
            _workerThread = null;
        }
    }

    public static Task<DiscordAudioSubsystem> CreateSubsystemAsync(IVoiceChannel vc, CancellationToken token = default)
    {
        Console.WriteLine("Creating subsystem {0}", vc.Id);
        return vc.ConnectAsync().ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                Console.WriteLine("Subsystem {0} created", vc.Id);
                return new DiscordAudioSubsystem(vc.Guild, vc, t.Result);
            }

            Console.WriteLine("Subsystem {1} creation error: {0}", t.Exception!.ToString(), vc.Id);
            throw t.Exception!;
        }, token);
    }
    
    public DiscordAudioSource Create()
    {
        return new DiscordAudioSource(this, _client.CreatePCMStream(AudioApplication.Music));
    }

    public void DeleteAudioSource(DiscordAudioSource src)
    {
        lock (_audios)
        {
            _audios.Remove(src);
            src.Dispose();
        }
    }

    public void AddAudioSource(DiscordAudioSource src)
    {
        lock (_audios)
        {
            if (!_audios.Contains(src))
            {
                _audios.Add(src);
                _processAudioEvent.Set();
            }
        }
    }

    public Task StopAllAsync()
    {
        var tcs = new TaskCompletionSource();
        
        lock (_audios)
        {
            if (_audios.Count == 0)
            {
                tcs.SetResult();
            }
            else
            {
                foreach (var audio in _audios)
                {
                    audio.Stop();
                }

                _ = Task.Run(() =>
                {
                    while(_audios.Count != 0) { Thread.Sleep(50); }
                    tcs.SetResult();
                });
            }
        }

        return tcs.Task;
    }


    private IAudioClient _client;
    private IVoiceChannel _vc;
    private IGuild _guild;
    private AutoResetEvent _processAudioEvent;
    private int _updateIndex;
    private List<DiscordAudioSource> _audios;
    private Thread? _workerThread;
    private int _pendingExit;
}