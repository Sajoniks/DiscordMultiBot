using System.Buffers;
using System.Diagnostics;
using DiscordMultiBot.App.Models.Audio2.Interface;

namespace DiscordMultiBot.App.Models.Audio2;

public record StreamingResource(Uri Source, bool Looping = false, float Volume = 1.0f);

public class FFmpegPcmDataProvider : IPcmDataProvider
{
    public FFmpegPcmDataProvider(StreamingResource resource)
    {
        _looping = resource.Looping;
        
        string pathToResource = System.Web.HttpUtility.UrlDecode(resource.Source.AbsolutePath);
        string volume = resource.Volume.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        
        var argsBuilder = new System.Text.StringBuilder();
        argsBuilder
            .Append("-hide_banner").Append(' ')
            .Append("-loglevel panic").Append(' ')
            .AppendFormat("-stream_loop {0}", resource.Looping ? -1 : 0).Append(' ')
            .AppendFormat("-i \"{0}\"", pathToResource).Append(' ')
            .AppendFormat("-af volume={0}", volume).Append(' ')
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
            throw new SystemException("Failed to start ffmpeg process");
        }

        _proc = proc;
        
        _inputStream = _proc.StandardOutput.BaseStream;
        _buffer = ArrayPool<byte>.Shared.Rent(65536);
        _bufferPos = 0;
        _eof = false;
    }
    
    public int Stream(int size)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FFmpegPcmDataProvider));
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
            var newBuffer = ArrayPool<byte>.Shared.Rent((_buffer.Length + size) * 2);
            Array.Copy(_buffer, newBuffer, _buffer.Length);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }

        _bufferPos = 0;
        
        int readBytes = 0;
        while (readBytes < size)
        {
            int read = _inputStream.Read(_buffer, _bufferPos,_buffer.Length - _bufferPos);
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
        if (_disposed) throw new ObjectDisposedException(nameof(FFmpegPcmDataProvider));
        _disposed = true;
        _bufferPos = 0;
        ArrayPool<byte>.Shared.Return(_buffer);
        _proc.Dispose();
    }

    public byte[] Buffer => _buffer;
    public bool Looping => _looping;
    public int BufferedSize => _bufferPos;
    public bool EndOfStream => _eof;

    private bool _disposed;
    private bool _eof;
    private bool _looping;
    private byte[] _buffer;
    private int _bufferPos;
    private Stream _inputStream;
    private Process _proc;
}
