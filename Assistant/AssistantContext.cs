namespace TwitchBot.Assistant
{        
    public class AssistantContext<T>
    {
        public T PreviousContext;
        public T CurrentContext;

        public AssistantContext(T initialContext)
        {
            PreviousContext = initialContext;
            CurrentContext = initialContext;
        }

        public void Update(T newContext)
        {
            PreviousContext = CurrentContext;
            CurrentContext = newContext;
        }
    }
}
