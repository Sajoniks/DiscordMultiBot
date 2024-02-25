using System.Diagnostics;
using System.Text;

namespace DiscordMultiBot.AudioPlayer.Model;

public class AudioPlayerWorker
{
    enum State
    {
        Stopped,
        Playing,
        Paused
    }
    
    private readonly Uri _source;
    private readonly StreamWriter _outputStream;
    private readonly StreamWriter _logger;

    private bool _hasExit;
    private State _state = State.Stopped;
    
    private TimeSpan _playTimeOffset = TimeSpan.Zero;
    private DateTime _playStartTimeUtc = DateTime.MinValue;
    
    private Process? _ffmpeg;

    public bool IsPlaying => _state == State.Playing;
    public bool IsStopped => _state == State.Stopped;
    public bool IsPaused => _state == State.Paused;

    
    public AudioPlayerWorker(Uri source, StreamWriter outputStream, StreamWriter logger)
    {
        _source = source;
        _outputStream = outputStream;
        _logger = logger;
    }
    
    public void Play(TimeSpan start)
    {
        if (IsPlaying || HasExited)
        {
            return;
        }
        
        if (_source.IsFile)
        {
            if (!File.Exists(_source.AbsolutePath))
            {
                throw new FileNotFoundException();
            }

            _logger.WriteLine("Playing/resuming audio");

            if (IsPaused)
            {
                InitFFmpeg(_playTimeOffset);
            }
            else
            {
                InitFFmpeg(start);
                _playTimeOffset = start;
                _playStartTimeUtc = DateTime.UtcNow + _playTimeOffset;
            }

            
            _state = State.Playing;
        }
    }

    private void InitFFmpeg(TimeSpan start)
    {
        if (_ffmpeg is null)
        {
            var procInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-hide_banner -i  \"{_source.AbsolutePath}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            _logger.WriteLine("Spawning FFmpeg process {0}", procInfo.Arguments);
            
            _ffmpeg = Process.Start(procInfo);

            if (_ffmpeg is null)
            {
                throw new NullReferenceException();
            }

            _ffmpeg.OutputDataReceived += FfmpegOnOutputDataReceived;
            _ffmpeg.Exited += FfmpegOnExited;
            _ffmpeg.EnableRaisingEvents = true;
            _ffmpeg.BeginOutputReadLine();
            
            _logger.WriteLine("Spawned FFMpeg process {0}", _ffmpeg.Id);
        }
    }

    private void FfmpegOnExited(object? sender, EventArgs e)
    {
        _logger.WriteLine("FFmpeg process exited code {0}", _ffmpeg?.ExitCode ?? -1);
        DisposeFFmpeg();
        _hasExit = true;
    }

    private void FfmpegOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            var bs = Encoding.UTF8.GetBytes(e.Data);
            _outputStream.Write(bs);
        }
    }

    public void Pause()
    {
        if (!IsPlaying || HasExited)
        {
            return;
        }
        
        _logger.WriteLine("Pausing audio");

        DisposeFFmpeg();
        _playTimeOffset = (DateTime.UtcNow - _playStartTimeUtc);
        _state = State.Paused;
    }

    public void Stop()
    {
        if (IsStopped || HasExited)
        {
            return;
        }

        _logger.WriteLine("Stopping audio");
        
        DisposeFFmpeg();
        _playStartTimeUtc = DateTime.MinValue;
        _state = State.Stopped;
    }

    private void DisposeFFmpeg()
    {
        if (_ffmpeg is not null)
        {
            _logger.WriteLine("Kill FFmpeg process");
            
            _ffmpeg.Kill(true);
            _ffmpeg.OutputDataReceived -= FfmpegOnOutputDataReceived;
            _ffmpeg.Exited -= FfmpegOnExited;
            _ffmpeg.Dispose();
            _ffmpeg = null;
        }
    }

    public void Forward(TimeSpan forwardAmount)
    {
        if (IsStopped || HasExited)
        {
            return;
        }
        
        _logger.WriteLine("Forwarding audio to {0}", forwardAmount);

        DisposeFFmpeg();
        if (IsPlaying)
        {
            _playTimeOffset = (DateTime.UtcNow - _playStartTimeUtc);
        }

        _playTimeOffset += forwardAmount;

        if (IsPlaying)
        {
            InitFFmpeg(_playTimeOffset);
            _playStartTimeUtc = DateTime.UtcNow + forwardAmount;
        }
    }

    public bool HasExited => _hasExit;
}