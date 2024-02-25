using System.Diagnostics;
using System.Globalization;
using System.Text;
using Discord;
using Discord.Audio;

namespace DiscordMultiBot.App.Models.Audio;


class AudioStreamPair
{
    public AudioStreamPair(long id, Stream inputStream, Stream outputStream, bool active = false, Action<long, Exception?>? callback= null)
    {
        Id = id;
        InputStream = inputStream;
        OutputStream = outputStream;
        Active = active;
        Callback = callback;
    }

    public long Id { get; }
    public Stream InputStream { get; }
    public Stream OutputStream { get; }
    public bool Active { get; set; }
    public Action<long, Exception?>? Callback { get; }
}

static class DiscordAudioThread
{
    private static readonly List<AudioStreamPair> Streams = new();
    
    static DiscordAudioThread()
    {
        var workerThread = new Thread(WorkerThread)
        {
            IsBackground = true,
            Name = "Discord Audio Thread"
        };
        workerThread.Start();
    }

    private static void WorkerThread()
    {
        byte[] buffer = new byte[65536];
        int streamIndex = -1;
        while (true)
        {
            if (Environment.HasShutdownStarted)
            {
                Console.WriteLine("DiscordAudioStream: Shutting down");
                return;
            }
            
            AudioStreamPair pair;
            lock (Streams)
            {
                if (Streams.Count == 0)
                {
                    continue;
                }

                streamIndex = (streamIndex + 1) % Streams.Count;
                pair = Streams[streamIndex];
            }

            if (!pair.Active)
            {
                continue;
            }
            
            try
            {
                int read = pair.InputStream.Read(buffer);
                pair.OutputStream.Write(buffer, 0, read);
            }
            catch (Exception e)
            {
                Console.WriteLine("DiscordAudioStream: Exception occured while processing stream [Id = {0}  Exception = {1}]", pair.Id, e);
                
                lock (Streams)
                {
                    try
                    {
                        int index = Streams.FindIndex(x => x.Id == pair.Id);
                        Streams.RemoveAt(index);
                        
                        Console.WriteLine("DiscordAudioStream: Removed stream due exception [Id = {0}  Total Streams = {1}]", pair.Id, Streams.Count);
                        
                        pair.Callback?.Invoke(pair.Id, e);
                    }
                    catch (Exception) { /**/ }
                }
            }
        }
    }

    public static long AddStreamPair(Stream input, Stream output, Action<long, Exception?>? callback = null)
    {
        lock (Streams)
        {
            Streams.Add(new AudioStreamPair(DateTime.Now.Ticks, input, output, callback: callback));
            Console.WriteLine("DiscordAudioStream: Added stream [Id = {0}  Total Streams = {1}]", Streams.Last().Id, Streams.Count);
            return Streams.Last().Id;
        }
    }

    public static void SetStreamPaused(long id, bool state)
    {
        lock (Streams)
        {
            try
            {
                int index = Streams.FindIndex(x => x.Id == id);
                Streams[index].Active = !state;
            }
            catch(Exception) { /**/ }
        }
    }

    public static bool RemoveStreamPair(long id)
    {
        lock (Streams)
        {
            try
            {
                int index = Streams.FindIndex(x => x.Id == id);
                var prev = Streams[index];
                Streams.RemoveAt(index);
                Console.WriteLine("DiscordAudioStream: Removed stream [Id = {0}  Total Streams = {1}]", prev.Id, Streams.Count);
                
                prev.Callback?.Invoke(id, null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

public class DiscordAudioPlayer : IAudioPlayerModel
{
    private class ConnectionManager : IDisposable
    {
        private enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
        }

        private Task _connectionTask = Task.CompletedTask;
        private IAudioClient? _audioClient;
        private IVoiceChannel? _voiceChannel;
        private CancellationTokenSource _connectCts = new();

        private ReaderWriterLockSlim _rwLock = new();
        private volatile ConnectionState _connectionState = ConnectionState.Disconnected;
        private volatile bool _disposed;

        public event Action<IAudioClient>? Connected;
        public event Action<Exception?>? Disconnected;

        public bool IsConnected => _connectionState == ConnectionState.Connected;
        public IVoiceChannel? VoiceChannel => _voiceChannel;
        
        public ConnectionManager()
        {
            _disposed = false;
        }

        private async Task ConnectToChannelAsyncInternal(IVoiceChannel voiceChannel)
        {
            _rwLock.EnterWriteLock();
            _connectionState = ConnectionState.Connecting;
            _rwLock.ExitWriteLock();

            try
            {
                _audioClient = await voiceChannel.ConnectAsync();
                _voiceChannel = voiceChannel;
                _audioClient.Disconnected += AudioClientOnDisconnected;
                _audioClient.ClientDisconnected += AudioClientOnClientDisconnected;

                _rwLock.EnterWriteLock();
                try
                {
                    _connectionState = ConnectionState.Connected;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }

                Connected?.Invoke(_audioClient);
            }
            catch (Exception e)
            {
                Console.WriteLine("Connection manager: Exception while connecting to channel: {0}", e);
                
                _rwLock.EnterWriteLock();
                _connectionState = ConnectionState.Disconnected;
                _rwLock.ExitWriteLock();

                _voiceChannel = null;
                
                throw;
            }
        }

        private Task AudioClientOnClientDisconnected(ulong arg)
        {
            Console.WriteLine("Connection manager: Client disconnected from the current channel");

            return Task.CompletedTask;
        }

        private Task AudioClientOnDisconnected(Exception arg)
        {
            Console.WriteLine("Connection manager: Audio client disconnected due exception: {0}", arg);
            
            if (_audioClient is not null)
            {
                _audioClient.Disconnected -= AudioClientOnDisconnected;
                _audioClient.ClientDisconnected -= AudioClientOnClientDisconnected;
                _audioClient.Dispose();
                _audioClient = null;
            }

            _voiceChannel = null;
            
            Disconnected?.Invoke(arg);
            return Task.CompletedTask;
        }

        public Task ConnectToChannelAsync(IVoiceChannel voiceChannel)
        {
            _rwLock.EnterUpgradeableReadLock();
            try
            {
                if (_connectionState == ConnectionState.Disconnected)
                {
                    _rwLock.EnterWriteLock();
                    try
                    {
                        _connectionState = ConnectionState.Connecting;
                    }
                    finally
                    {
                        _rwLock.ExitWriteLock();
                    }

                    if (_connectCts.IsCancellationRequested)
                    {
                        _connectCts.Dispose();
                        _connectCts = new CancellationTokenSource();
                    }

                    _connectCts.Token.Register(() =>
                    {
                        Console.WriteLine("DiscordAudioPlayer: Connection task was cancelled");
                        
                        _rwLock.EnterWriteLock();
                        _connectionState = ConnectionState.Disconnected;
                        _rwLock.ExitWriteLock();
                    });
                    _connectionTask = Task.Run(() => ConnectToChannelAsyncInternal(voiceChannel), _connectCts.Token);
                }

                return _connectionTask;
            }
            finally
            {
                _rwLock.ExitUpgradeableReadLock();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                _audioClient?.Dispose();
                
                _connectCts.Cancel();
                _connectCts.Dispose();
                
                _rwLock.Dispose();
                _connectionState = ConnectionState.Disconnected;
            }
        }
    }

    private enum State
    {
        Stopped,
        Playing,
        Paused
    }
    
    private readonly ConnectionManager _connectionManager = new();
    private readonly object _syncObject = new();
    
    private Process? _ffmpeg;
    private Stream? _inputStream;
    private Stream? _outputStream;

    private volatile bool _disposed;
    private volatile State _workerState = State.Stopped;
    private volatile bool _playRequested;
    private long _workerId = -1;

    public Action<Exception?>? Disconnected;
    public Action<Exception?>? PlaybackFinished;
    
    public DiscordAudioPlayer()
    {
        _disposed = false;
        _playRequested = false;
        _connectionManager.Connected += ConnectionManagerOnConnected;
        _connectionManager.Disconnected += ConnectionManagerOnDisconnected;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DiscordAudioPlayer));
    }

    private void CreateProcess(AudioParameters parms)
    {
        lock (_syncObject)
        {
            if (_ffmpeg is not null)
            {
                Console.WriteLine("Audio player: destroying previous ffmpeg process");

                CloseProcess();
            }
        }

        var argsBuilder = new StringBuilder();
        argsBuilder
            .Append("-hide_banner").Append(' ')
            .Append("-loglevel panic").Append(' ')
            .AppendFormat("-stream_loop {0}", parms.Looping ? -1 : 0).Append(' ')
            .AppendFormat("-i \"{0}\"", System.Web.HttpUtility.UrlDecode(parms.Uri.AbsolutePath)).Append(' ')
            .AppendFormat("-af volume={0}", parms.Volume.ToString("F1", CultureInfo.InvariantCulture)).Append(' ')
            .Append("-ac 2").Append(' ')
            .Append("-f s16le").Append(' ')
            .Append("-ar 48000").Append(' ')
            .Append("pipe:1");
        
        
        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = argsBuilder.ToString(),
            UseShellExecute = false,
            RedirectStandardOutput = true
        });

        if (proc is null)
        {
            Console.WriteLine("Audio player: failed to start ffmpeg process");
            throw new NullReferenceException("Worker process failed to start");
        }

        lock (_syncObject)
        {
            _ffmpeg = proc;
            _inputStream = _ffmpeg.StandardOutput.BaseStream;
                
            if (_inputStream is not null && _outputStream is not null)
            {
                _workerId = DiscordAudioThread.AddStreamPair(_inputStream, _outputStream, StreamPlaybackFinished);
            }
                
            _ffmpeg.EnableRaisingEvents = true;
            _ffmpeg.Exited += PlayerProcessOnExited;
        }

        Console.WriteLine("Audio player: Process has started");
    }

    private void StreamPlaybackFinished(long arg1, Exception? arg2)
    {
        if (arg2 is not null)
        {
            Console.WriteLine("Audio player: Playback finished due exception: {0} [Id={1}]", arg2, arg1);
        }

        lock (_syncObject)
        {
            CloseProcess();

            if (_workerId == arg1)
            {
                _workerId = -1;
                _workerState = State.Stopped;
            }
        }
        
        PlaybackFinished?.Invoke(arg2);
    }

    private void CloseProcess()
    {
        if (_ffmpeg is not null)
        {
            _inputStream = null;
                
            _ffmpeg.EnableRaisingEvents = false;
            _ffmpeg.Exited -= PlayerProcessOnExited;
            _ffmpeg.Kill();
            _ffmpeg.Close();
            _ffmpeg.Dispose();
            _ffmpeg = null;
        }
    }
    
    private void PlayerProcessOnExited(object? sender, EventArgs e)
    {
        lock (_syncObject)
        {
            Console.WriteLine("Audio player: ffmpeg has exited");

            if (ReferenceEquals(sender, _ffmpeg))
            {
                var prevWorker = _workerId;
                CloseProcess();

                if (DiscordAudioThread.RemoveStreamPair(_workerId))
                {
                    if (prevWorker != _workerId)
                    {
                        return; // worker has been closed somewhere else after process was closed
                    }
                    
                    _workerId = -1;
                    _workerState = State.Stopped;
                }
            }
        }
    }
    
    private void ConnectionManagerOnDisconnected(Exception? obj)
    {
        lock (_syncObject)
        {
            _outputStream = null;

            if (DiscordAudioThread.RemoveStreamPair(_workerId))
            {
                _workerId = -1;
                _workerState = State.Stopped;
            }
        }
        
        Disconnected?.Invoke(obj);
    }

    private void ConnectionManagerOnConnected(IAudioClient obj)
    {
        lock (_syncObject)
        {
            _outputStream = obj.CreatePCMStream(AudioApplication.Music);
            
            if (_inputStream is not null && _outputStream is not null)
            {
                _workerId = DiscordAudioThread.AddStreamPair(_inputStream, _outputStream, StreamPlaybackFinished);
                if (_playRequested)
                {
                    _playRequested = false;
                    _workerState = State.Playing;
                    DiscordAudioThread.SetStreamPaused(_workerId, false);
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            lock (_syncObject)
            {
                if (_ffmpeg is not null)
                {
                    _ffmpeg.EnableRaisingEvents = false;
                    _ffmpeg.Exited -= PlayerProcessOnExited;
                    _ffmpeg.Kill();
                    _ffmpeg.Close();
                    _ffmpeg.Dispose();
                    _ffmpeg = null;
                }

                _inputStream = null;
                _outputStream = null;
                
                _connectionManager.Dispose();
            }
        }
    }

    public bool IsPlaying => _workerState == State.Playing;
    public bool IsPaused => _workerState == State.Paused;
    public bool IsStopped => _workerState == State.Stopped;
    public IVoiceChannel? ConnectedChannel => _connectionManager.VoiceChannel;
    
    public Task AttachToChannelAsync(IVoiceChannel voiceChannel)
    {
        ThrowIfDisposed();

        return _connectionManager.ConnectToChannelAsync(voiceChannel);
    }
    
    public void Open(AudioParameters parameters)
    {
        ThrowIfDisposed();

        Console.WriteLine("Audio player: Call open [Path = {0}]", parameters.Uri.AbsolutePath);

        CreateProcess(parameters);
    }

    public void Play(TimeSpan start)
    {
        ThrowIfDisposed();

        lock (_syncObject)
        {
            if (_inputStream is not null && _outputStream is not null)
            {
                Console.WriteLine("Audio player: Playing");

                _workerState = State.Playing;
                DiscordAudioThread.SetStreamPaused(_workerId, false);
            }
            else
            {
                _playRequested = true;
            }
        }
    }

    public void Pause()
    {
        ThrowIfDisposed();

        lock (_syncObject)
        {
            if (_workerState == State.Playing)
            {
                Console.WriteLine("Audio player: Paused player");

                DiscordAudioThread.SetStreamPaused(_workerId, true);
                _workerState = State.Paused;
            }

            _playRequested = false;
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();

        lock (_syncObject)
        {
            Console.WriteLine("Audio player: Stopped player");

            _playRequested = false;
            _workerState = State.Stopped;

            if (_ffmpeg is not null)
            {
                CloseProcess();
            }
        }
    }

    public void Forward(TimeSpan forwardAmount)
    {
        ThrowIfDisposed();
        
        // do nothing
    }

    public int Id => -1;
}