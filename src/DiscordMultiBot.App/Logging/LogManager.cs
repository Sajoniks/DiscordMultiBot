using Discord;
using Discord.Logging;

namespace DiscordMultiBot.App.Logging
{
    public class LogManager
    {
        public LogSeverity Level { get; }

        public event Func<LogMessage, Task>? Message;

        public LogManager(LogSeverity minSeverity)
        {
            Level = minSeverity;
        }

        public void Log(LogSeverity severity, string source, Exception ex)
        {
            try
            {
                if (severity <= Level)
                    Message?.Invoke(new LogMessage(severity, source, null, ex));
            }
            catch
            {
                // ignored
            }
        }
        public void Log(LogSeverity severity, string source, string message, Exception ex = null)
        {
            try
            {
                if (severity <= Level)
                    Message?.Invoke(new LogMessage(severity, source, message, ex));
            }
            catch
            {
                // ignored
            }
        }

        public void Log(LogSeverity severity, string source, FormattableString message, Exception ex = null)
        {
            try
            {
                if (severity <= Level)
                    Message?.Invoke(new LogMessage(severity, source, message.ToString(), ex));
            }
            catch { }
        }


        public void Error(string source, Exception ex)
            => Log(LogSeverity.Error, source, ex);
        public void Error(string source, string message, Exception ex = null)
            => Log(LogSeverity.Error, source, message, ex);

        public void Error(string source, FormattableString message, Exception ex = null)
            => Log(LogSeverity.Error, source, message, ex);


        public void WarningAsync(string source, Exception ex)
            => Log(LogSeverity.Warning, source, ex);
        public void WarningAsync(string source, string message, Exception ex = null)
            => Log(LogSeverity.Warning, source, message, ex);

        public void WarningAsync(string source, FormattableString message, Exception ex = null)
            => Log(LogSeverity.Warning, source, message, ex);


        public void Info(string source, Exception ex)
            => Log(LogSeverity.Info, source, ex);
        public void Info(string source, string message, Exception ex = null)
            => Log(LogSeverity.Info, source, message, ex);
        public void Info(string source, FormattableString message, Exception ex = null)
            => Log(LogSeverity.Info, source, message, ex);


        public void Verbose(string source, Exception ex)
            => Log(LogSeverity.Verbose, source, ex);
        public void Verbose(string source, string message, Exception ex = null)
            => Log(LogSeverity.Verbose, source, message, ex);
        public void Verbose(string source, FormattableString message, Exception ex = null)
            => Log(LogSeverity.Verbose, source, message, ex);


        public void Debug(string source, Exception ex)
            => Log(LogSeverity.Debug, source, ex);
        public void Debug(string source, string message, Exception ex = null)
            => Log(LogSeverity.Debug, source, message, ex);
        public void Debug(string source, FormattableString message, Exception ex = null)
            => Log(LogSeverity.Debug, source, message, ex);


        public Logger CreateLogger(string name) => new Logger(this, name);
    }
}
