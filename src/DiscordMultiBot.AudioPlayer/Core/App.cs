using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DiscordMultiBot.AudioPlayer.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordMultiBot.AudioPlayer.Core;

public class App
{
    private class NetworkStreamReader
    {
        private readonly NetworkStream _networkStream;
        private readonly byte[] _buffer;
        
        public NetworkStreamReader(NetworkStream networkStream)
        {
            _networkStream = networkStream;
            _buffer = new byte[512];
        }

        public string Read()
        {
            int x = _networkStream.Read(_buffer);
            return Encoding.UTF8.GetString(_buffer, 0, x);
        }
    }

    private class NetworkStreamWriter : Stream
    {
        private readonly NetworkStream _networkStream;

        public NetworkStreamWriter(NetworkStream networkStream)
        {
            _networkStream = networkStream;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _networkStream.Write(buffer, offset, count);
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    private readonly Socket _socket;
    private readonly IPEndPoint _endPoint;
    private readonly StreamWriter _logFileStream;
    
    public App(int port)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

        string logsDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        string logFilePath = Path.Combine(logsDirPath,
            $"aupl_{Process.GetCurrentProcess().Id}_{DateTime.UtcNow.ToString("yy-MM-dd_ss")}.txt");

        if (!Directory.Exists(logsDirPath))
        {
            Directory.CreateDirectory(logsDirPath);
        }
        
        _logFileStream = new StreamWriter(new FileStream(
            logFilePath,
            FileMode.OpenOrCreate
        ));
        _logFileStream.AutoFlush = true;
        _logFileStream.WriteLine("Log file opened at {0}", ((FileStream)_logFileStream.BaseStream).Name);

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _endPoint = new IPEndPoint(IPAddress.Loopback, port);
    }

    private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logFileStream.WriteLine("Unhandled exception: {0}", e.ExceptionObject);
        Environment.Exit(-1);
    }

    public void Start(Uri source)
    {
        _logFileStream.WriteLine("Starting app {0}", _endPoint);
        _socket.Connect(_endPoint);
        _logFileStream.WriteLine("Connected to process at {0} [Endpoint = {1}]", _socket.RemoteEndPoint, _socket.LocalEndPoint);

        using var s = new NetworkStream(_socket, FileAccess.ReadWrite, false);
        var reader = new NetworkStreamReader(s);
        using var writer = new StreamWriter( new NetworkStreamWriter(s) );
        
        AudioPlayerWorker worker;

        try
        {
            _logFileStream.WriteLine("Creating player worker source file {0}", source.AbsolutePath);
            worker = new AudioPlayerWorker(source, writer,_logFileStream);
        }
        catch (Exception e)
        {
            _logFileStream.WriteLine("Failed to open source file {0}: {1}", source.AbsolutePath, e);
            throw;
        }

        while (true)
        {
            if (worker.HasExited)
            {
                throw new Exception("Worker has exited");
            }
            
            if (_socket.Poll(0, SelectMode.SelectRead))
            {
                string input = reader.Read();
                try
                {
                    JObject? message = JsonConvert.DeserializeObject<JObject>(input);

                    if (message is not null)
                    {
                        _logFileStream.WriteLine("Received message: {0}", input);

                        if (message.TryGetValue("action", out var actionValue))
                        {
                            string action = actionValue.Value<string>() ?? "";

                            if (action.Equals("play") && message.TryGetValue("start", out var playStartValue))
                            {
                                TimeSpan start = TimeSpan.FromSeconds(playStartValue.Value<double>());
                                worker.Play(start);
                            }
                            else if (action.Equals("pause"))
                            {
                                worker.Pause();
                            }
                            else if (action.Equals("stop"))
                            {
                                worker.Stop();
                            }
                            else if (action.Equals("forward") && message.TryGetValue("time", out var forwardValue))
                            {
                                TimeSpan forward = TimeSpan.FromSeconds(forwardValue.Value<double>());
                                worker.Forward(forward);
                            }
                        }
                    }
                    else
                    {
                        _logFileStream.WriteLine("Failed to parser message. Ignore");
                    }
                }
                catch (Exception e)
                {
                    _logFileStream.WriteLine("Exception while parse: {0}", e);
                }
            }
        }
    }
}