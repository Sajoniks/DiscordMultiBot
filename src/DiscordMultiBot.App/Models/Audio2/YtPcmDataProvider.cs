using System.Buffers;
using System.Diagnostics;
using System.Text;
using DiscordMultiBot.App.Models.Audio2.Interface;

namespace DiscordMultiBot.App.Models.Audio2;

public class YtPcmDataProvider : IPcmDataProvider
{
    public YtPcmDataProvider(Uri uri)
    {
        if (!uri.Host.Equals("www.youtube.com"))
        {
            throw new ArgumentException("URI is not a youtube");
        }

        var ytDlpPath = Environment.GetEnvironmentVariable("YT_DLP_PATH");
        if (ytDlpPath is null || ytDlpPath.Length == 0)
        {
            throw new FileNotFoundException("yt-dlp was not found");
        }
        
        var argsList = new[]
        {
            "yt-dlp",
            "--quiet", "-f", "worstaudio", "-o", "-", $"\"{uri}\"", "|",
            "ffmpeg", 
            "-hide_banner",
            "-loglevel", "panic",
            "-i", "-",
            "-ac", "2",
            "-f", "s16le",
            "-ar", "48000", "-"
        };

        Process? proc;
        if (File.Exists("/bin/bash"))
        {
            // Bash
            proc = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"./{String.Join(' ', argsList).Replace("\"", "\\\"")}\"",
                WorkingDirectory = ytDlpPath,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        else
        {
            // Cmd
            proc = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + String.Join(' ', argsList),
                WorkingDirectory = ytDlpPath,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        if (proc is null)
        {
            throw new SystemException("Failed to start ffmpeg process");
        }

        _proc = proc;
        _inputStream = _proc.StandardOutput.BaseStream;
        _buffer = ArrayPool<byte>.Shared.Rent(65536);
        _bufferPos = 0;
        _eof = false;
        _disposed = false;
    }

    public int Stream(int size)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(YtPcmDataProvider));
        }

        if (EndOfStream)
        {
            throw new EndOfStreamException();
        }

        if (_proc.HasExited)
        {
            _eof = true;
            return 0;
        }

        if (size > _buffer.Length)
        {
            // realloc
        }

        _bufferPos = 0;
        int readBytes = 0;
        while (readBytes < size)
        {
            int read = _inputStream.Read(_buffer, _bufferPos, _buffer.Length - _bufferPos);
            _bufferPos += read;
            readBytes += read;

            if (read == 0)
            {
                _eof = true;
                break;
            }
        }

        return readBytes;
    }
    
    public void Dispose()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(YtPcmDataProvider));
        _disposed = true;
        _proc.Dispose();
        _bufferPos = 0;
        _eof = true;
    }

    public bool EndOfStream => _eof;
    public byte[] Buffer => _buffer;
    public bool Looping => false;
    public int BufferedSize => _bufferPos;

    private Process _proc;
    private Stream _inputStream;
    private byte[] _buffer;
    private int _bufferPos;
    private bool _eof;
    private bool _disposed;
}