using System.Globalization;

namespace TwitchBot
{
    public class Logger
    {
        private readonly string Name;
        public static readonly CultureInfo LOG_FORMAT = new("en-GB");
        public static ConsoleLogLevel Level = ConsoleLogLevel.Info;

        public Logger(string name)
        {
            Name = name;
        }

        public void Log(string message, ConsoleLogLevel level)
        {
            var timestamp = DateTime.Now.ToString(LOG_FORMAT);
            Console.WriteLine($"{timestamp} [{Name}] [{level}] {message}");
        }

        public void Debug(string message)
        {
            if (Level <= ConsoleLogLevel.Debug)
            {
                var timestamp = DateTime.Now.ToString(LOG_FORMAT);
                Console.WriteLine($"{timestamp} [{Name}] [DEBUG] {message}");
            }
        }

        public void Info(string message)
        {
            if (Level <= ConsoleLogLevel.Info)
            {
                var timestamp = DateTime.Now.ToString(LOG_FORMAT);
                Console.WriteLine($"{timestamp} [{Name}] {message}");
            }
        }

        public void Error(string message)
        {
            if (Level <= ConsoleLogLevel.Error)
            {
                var timestamp = DateTime.Now.ToString(LOG_FORMAT);
                Console.WriteLine($"{timestamp} [{Name}] [ERROR] {message}");
            }
        }
    }

    public enum ConsoleLogLevel
    {
        Debug,
        Info,
        Error
    }
}
