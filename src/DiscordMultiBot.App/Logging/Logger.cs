using Discord;

namespace DiscordMultiBot.App.Logging
{
    public class Logger
    {
        private readonly LogManager _manager;

        public string Name { get; }
        public LogSeverity Level => _manager.Level;

        public Logger(LogManager manager, string name)
        {
            _manager = manager;
            Name = name;
        }

        public void Log(LogSeverity severity, Exception exception = null)
            => _manager.Log(severity, Name, exception);
        public void Log(LogSeverity severity, string message, Exception exception = null)
            => _manager.Log(severity, Name, message, exception);
        public void Log(LogSeverity severity, FormattableString message, Exception exception = null)
            => _manager.Log(severity, Name, message, exception);


        public void Error(Exception exception)
            => _manager.Error(Name, exception);
        public void Error(string message, Exception exception = null)
            => _manager.Error(Name, message, exception);

        public void Error(FormattableString message, Exception exception = null)
            => _manager.Error(Name, message, exception);


        public void Warning(Exception exception)
            => _manager.WarningAsync(Name, exception);
        public void WarningAsync(string message, Exception exception = null)
            => _manager.WarningAsync(Name, message, exception);

        public void WarningAsync(FormattableString message, Exception exception = null)
            => _manager.WarningAsync(Name, message, exception);


        public void Info(Exception exception)
            => _manager.Info(Name, exception);
        public void Info(string message, Exception exception = null)
            => _manager.Info(Name, message, exception);

        public void Info(FormattableString message, Exception exception = null)
            => _manager.Info(Name, message, exception);


        public void Verbose(Exception exception)
            => _manager.Verbose(Name, exception);
        public void Verbose(string message, Exception exception = null)
            => _manager.Verbose(Name, message, exception);

        public void Verbose(FormattableString message, Exception exception = null)
            => _manager.Verbose(Name, message, exception);


        public void Debug(Exception exception)
            => _manager.Debug(Name, exception);
        public void Debug(string message, Exception exception = null)
            => _manager.Debug(Name, message, exception);

        public void Debug(FormattableString message, Exception exception = null)
            => _manager.Debug(Name, message, exception);

    }
}
