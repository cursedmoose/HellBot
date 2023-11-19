using System.Globalization;
using System.Speech.Recognition;

namespace TwitchBot.SpeechToText
{
    public class SpeechToText
    {
        private SpeechRecognitionEngine recognizer;
        private Logger Log = new("Speech");
        public static bool Enabled { get; private set; }
        public int Rejections { get; private set; } = 0;
        public int Accepts { get; private set; } = 0;

        public SpeechToText(bool enabled)
        {
            recognizer = new(new CultureInfo("en-US"));
            
            Log.Info("Configuring Speech Recognition Engine..");
            var grammar = CreateHellbotGrammar();
            recognizer.LoadGrammar(grammar);

            recognizer.SpeechRecognized += onSpeechRecognized;
            recognizer.SetInputToDefaultAudioDevice();
            Log.Info($"Silence Timeout: {recognizer.InitialSilenceTimeout}");
            Log.Info($"Babble Timeout: {recognizer.BabbleTimeout}");
            Log.Info($"EndSilence Timeout: {recognizer.EndSilenceTimeout}");
            Log.Info($"EndSilenceAmbiguous Timeout: {recognizer.EndSilenceTimeoutAmbiguous}");

            Log.Info("Starting Speech Recognition Engine..");

            if (enabled)
            {
                recognizer.RecognizeAsync(RecognizeMode.Multiple);
            }
        }


        public async void onSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence > 0.94f)
            {
                try
                {
                    var command = e.Result.Text.Replace("hey hellbot", "").Replace("hey sheogorath", "").Trim();
                    Console.WriteLine($"Confidence: {e.Result.Confidence}%, trying to {command} ({++Accepts} / {Accepts + Rejections})");
                    if (command.Contains("say hello")) 
                    {
                        await Server.Instance.chatgpt.GetResponse(Server.Instance.Assistant.Persona, "hello");
                    } 
                    else if (command.Contains("what is this"))
                    {
                        await Server.Instance.Assistant.ReactToCurrentScreen();
                    }
                    else if (command.Contains("roll the dice"))
                    {
                        var result = new Random().Next(1, 21);
                        Server.Instance.Assistant.PlayTts($"Looks like you rolled a {result}");
                    }
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                Rejections++;
            }
        }

        static Grammar CreateHellbotGrammar()
        {
            // Create a grammar for finding services in different cities.  
            Choices hellbot = new Choices(new string[] { "hellbot", "sheogorath" });
            Choices hellbotCommands = new Choices(new string[] { "what is this", "roll the dice" });

            GrammarBuilder findServices = new GrammarBuilder("hey");
            findServices.Append(hellbot);
            findServices.Append(hellbotCommands);

            // Create a Grammar object from the GrammarBuilder. 
            Grammar servicesGrammar = new Grammar(findServices);
            servicesGrammar.Name = ("Hellbot");
            return servicesGrammar;
        }
    }
}
