using System.Diagnostics;
using System.Globalization;
using System.Text;
using Discord;
using Discord.Audio;

namespace DiscordMultiBot.App.Models.Audio;

public class DiscordBotAudioPlayer : IAudioPlayerModel
{
    // @todo
    // Make buffering from file in order to allow playback etc...
    // For now it buffers file COMPLETELY 

    public enum PlayerMode
    {
        Stopped,
        Playing,
        Paused
    }

    public enum ConnectionMode
    {
        Disconnected,
        Connecting,
        Connected
    }

    private Process? _ffmpegProcess;
    
    private volatile PlayerMode _mode = PlayerMode.Stopped;
    private volatile ConnectionMode _connectionMode = ConnectionMode.Disconnected;
    private volatile bool _disposed;
    private volatile bool _isWriting;
    
    private AudioOutStream? _audioOutStream = null;

    private Task _connectTask = Task.CompletedTask;
    private Task _sendTask = Task.CompletedTask;
    private CancellationTokenSource _connectCts = new();
    private CancellationTokenSource _sendCts = new();
    
    private SemaphoreSlim _ffmpegReaderSemaphore = new(1);
    
    private volatile Stream? _ffmpegOutput;
    private volatile IVoiceChannel? _vc;
    private volatile IAudioClient? _audioClient;
    
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DiscordBotAudioPlayer));
    }

    public DiscordBotAudioPlayer()
    {
    }

    public event EventHandler<Exception?>? PlaybackFinished;
    public event EventHandler<Exception?>? Disconnected; 
    public event EventHandler? VoiceChannelEmpty; 

    public bool IsAttachedToChannel => _connectionMode == ConnectionMode.Connected;
    public IVoiceChannel? AttachedToChannel => _vc;

    public bool IsPlaying => IsAttachedToChannel && _mode == PlayerMode.Playing && _isWriting;
    public bool IsPaused => IsAttachedToChannel && _mode == PlayerMode.Paused && _isWriting;

    private void CreatePlayerProcess(AudioParameters parameters)
    {
        if (_ffmpegProcess is not null)
        {
            Console.WriteLine("Audio player: destroying previous ffmpeg process");
            try
            {
                _ffmpegReaderSemaphore.Wait();
                _ffmpegOutput = null;
            }
            finally
            {
                _ffmpegReaderSemaphore.Release();
            }
            
            _ffmpegProcess.Kill();
            _ffmpegProcess.Close();
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
        }

        var argsBuilder = new StringBuilder();
        argsBuilder
            .Append("-hide_banner").Append(' ')
            .Append("-loglevel panic").Append(' ')
            .AppendFormat("-stream_loop {0}", parameters.Looping ? -1 : 0).Append(' ')
            .AppendFormat("-i \"{0}\"", System.Web.HttpUtility.UrlDecode(parameters.Uri.AbsolutePath)).Append(' ')
            .AppendFormat("-af volume={0}", parameters.Volume.ToString("F1", CultureInfo.InvariantCulture)).Append(' ')
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

        _ffmpegProcess = proc;

        try
        {
            _ffmpegReaderSemaphore.Wait();
            _ffmpegOutput = _ffmpegProcess.StandardOutput.BaseStream;
        }
        finally
        {
            _ffmpegReaderSemaphore.Release();
        }

        Console.WriteLine("Audio player process has started");
        
        _ffmpegProcess.EnableRaisingEvents = true;
        _ffmpegProcess.Exited += PlayerProcessOnExited;
    }

    private void PlayerProcessOnExited(object? sender, EventArgs e)
    {
        Console.WriteLine("Audio player process has exited");
        _sendCts.Cancel();

        try
        {
            _ffmpegReaderSemaphore.Wait();
            _ffmpegOutput = null;
            
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
        }
        finally
        {
            _ffmpegReaderSemaphore.Release();
        }
    }

    private void InvokeOnFinished(Exception? e)
    {
        PlaybackFinished?.Invoke(this, e);
    }
    
    private void WriteToAudioStream(CancellationToken token)
    {
        if (_ffmpegProcess is null || _audioClient is null)
        {
            Console.WriteLine("Audio player: failed to start play");
            return;
        }
        
        var buffer = new byte[65536];
        using var memStream = new MemoryStream(65536);
        bool memStreamFlushed = false;

        try
        {
            _audioOutStream?.Dispose();
            _audioOutStream = _audioClient.CreatePCMStream(AudioApplication.Music);
        }
        catch (Exception e)
        {
            Console.WriteLine("Audio player exception: {0}", e);
            return;
        }

        while (true)
        {
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("Audio player: cancellation requested");
                return;
            }

            while (_mode == PlayerMode.Paused)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("Audio player: cancellation requested");
                    return;
                }

                try
                {
                    _audioOutStream?.Flush();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Audio player exception: {0}", arg: e);
                    return;
                }

                Thread.Sleep(100);
            }

            int readBytes = 0;
            try
            {
                _ffmpegReaderSemaphore.Wait(token);

                if (_ffmpegOutput is null)
                {
                    continue;
                }

                readBytes = _ffmpegOutput
                    .ReadAsync(buffer, token)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine("Audio player: cancellation requested");
                return;
            }
            finally
            {
                _ffmpegReaderSemaphore.Release();
            }

            if (readBytes > 0)
            {
                if (_connectionMode == ConnectionMode.Connected)
                {
                    try
                    {
                        if (!memStreamFlushed)
                        {
                            _audioOutStream?
                                .WriteAsync(memStream.ToArray(), token)
                                .GetAwaiter()
                                .GetResult();
                            
                            memStreamFlushed = true;
                        }
                        
                        _audioOutStream?
                            .WriteAsync(buffer, 0, readBytes, token)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (OperationCanceledException e)
                    {
                        Console.WriteLine("Audio player: cancellation requested");
                        try
                        {
                            _audioOutStream?.Flush();
                        }
                        catch (Exception)
                        {
                            // ignore
                        }
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Audio player exception: {0}", e);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        memStream
                            .WriteAsync(buffer, 0, readBytes, token)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (OperationCanceledException e)
                    {
                        Console.WriteLine("Audio player: cancellation requested");

                        try
                        {
                            _audioOutStream?.Flush();
                        }
                        catch (Exception)
                        {
                            // ignore
                        }

                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Audio player exception: {0}", e);
                        return;
                    }
                }
            }
        }
    }

    private async Task ConnectToChannelAsync(IVoiceChannel voiceChannel)
    {
        try
        {
            _audioClient = await voiceChannel.ConnectAsync();
            _vc = voiceChannel;
            _audioClient.ClientDisconnected += AudioClientOnClientDisconnected;
            _audioClient.Disconnected += AudioClientOnDisconnected;
        }
        catch (Exception e)
        {
            Console.WriteLine("Audio player exception: {0}", arg: e);
            _connectCts.Cancel();
        }
    }
    
    private void BeginWriteToAudioStreamAsync()
    {
        if (!_isWriting)
        {
            _isWriting = true;
            if (_sendCts.IsCancellationRequested)
            {
                _sendCts = new CancellationTokenSource();
            }

            _sendCts.Token.Register(() =>
            {
                _isWriting = false;
            });
            _sendTask = Task.Run(() => WriteToAudioStream(_sendCts.Token), _sendCts.Token)
                .ContinueWith(t =>
                {
                    _isWriting = false;
                    _mode = PlayerMode.Stopped;
                    InvokeOnFinished(t.Exception?.InnerException);
                });
        }
    }
    
    private Task AudioClientOnDisconnected(Exception arg)
    {
        Console.WriteLine("Audio player exception: {0}", arg: arg);
        Disconnected?.Invoke(this, arg);
        return Task.CompletedTask;
    }

    private async Task AudioClientOnClientDisconnected(ulong arg)
    {
        if (_vc is not null)
        {
            var users = await _vc.Guild.GetUsersAsync();
            bool empty = !users.Any(u => (u.VoiceChannel is not null && u.VoiceChannel.Id == _vc.Id));

            if (empty)
            {
                VoiceChannelEmpty?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    public Task AttachToChannelAsync(IVoiceChannel voiceChannel)
    {
        ThrowIfDisposed();

        if (_connectionMode == ConnectionMode.Disconnected)
        {
            _connectionMode = ConnectionMode.Connecting;
            
            if (_connectCts.IsCancellationRequested)
            {
                _connectCts.Dispose();
                _connectCts = new CancellationTokenSource();
            }

            _connectCts.Token.Register(() =>
            {
                _mode = PlayerMode.Stopped;
                _connectionMode = ConnectionMode.Disconnected;
                _vc = null;
                _audioClient?.Dispose();
                _audioClient = null;
            });
            _connectTask = Task.Run(() => ConnectToChannelAsync(voiceChannel), _connectCts.Token)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        _connectionMode = ConnectionMode.Connected;

                        if (_mode == PlayerMode.Playing && !_isWriting)
                        {
                            BeginWriteToAudioStreamAsync();
                        }
                    }
                });
        }
        
        return _connectTask;
    }

    public void Open(AudioParameters parameters)
    {
        ThrowIfDisposed();

        CreatePlayerProcess(parameters);
    }

    public void Play(TimeSpan start)
    {
        ThrowIfDisposed();

        if (_mode != PlayerMode.Playing)
        {
            _mode = PlayerMode.Playing;
            if (_connectionMode == ConnectionMode.Connected && !_isWriting)
            {
                BeginWriteToAudioStreamAsync();
            }
        }
    }

    public void Pause()
    {
        ThrowIfDisposed();

        if (_mode == PlayerMode.Playing)
        {
            _mode = PlayerMode.Paused;
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();

        _mode = PlayerMode.Stopped;
    }

    public void Forward(TimeSpan forwardAmount)
    {
        ThrowIfDisposed();
    }

    public int Id => _ffmpegProcess?.Id ?? -1;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            
            _sendCts.Cancel();
            _sendCts.Dispose();
            
            _connectCts.Cancel();
            _connectCts.Dispose();

            _audioClient?.Dispose();
            _audioClient = null;
        }
    }
}