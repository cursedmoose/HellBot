namespace TwitchBot.Twitch.Model
{
    internal enum CommemorationStatus
    {
        NotStarted = 0,
        InProgress,
        Ended
    }
    public class Commemoration
    {
        CommemorationStatus Status;
        public string Event;
        public TwitchUser Organizer;
        public ICollection<TwitchUser> Observers;

        public Commemoration(string excitingEvent, TwitchUser organizer)
        {
            Status = CommemorationStatus.NotStarted;
            Organizer = organizer;
            Event = excitingEvent;
            Observers = new HashSet<TwitchUser>();
        }

        public bool InProgress()
        {
            return Status == CommemorationStatus.InProgress;
        }

        public void Start()
        {
            Status = CommemorationStatus.InProgress;
        }

        public void Stop()
        {
            Status = CommemorationStatus.Ended;
        }
    }
}
