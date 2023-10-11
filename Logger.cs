namespace TwitchBot
{
    public class Logger
    {
        private string Name;

        public Logger(string name)
        {
            Name = name;
        }

        public void info(string message)
        {
            var timestamp = DateTime.Now.ToString(Server.LOG_FORMAT);
            Console.WriteLine($"{timestamp} [{Name}] {message}");
        }
    }
}
