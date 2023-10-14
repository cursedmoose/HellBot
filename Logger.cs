namespace TwitchBot
{
    public class Logger
    {
        private readonly string Name;

        public Logger(string name)
        {
            Name = name;
        }

        public void Info(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [{Name}] {message}");
        }

        public void Error(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [{Name}] [ERROR] {message}");
        }
    }
}
