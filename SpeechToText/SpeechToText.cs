﻿using System.Globalization;
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

        private AudioState CurrentAudioState = AudioState.Silence;

        public SpeechToText(bool enabled)
        {
            recognizer = new(new CultureInfo("en-US"));

            if (enabled)
            {
                Log.Info("Configuring Speech Recognition Engine..");
                var grammar = CreateHellbotGrammar();
                recognizer.LoadGrammar(grammar);
                var werner = CreateWernerGrammar();
                recognizer.LoadGrammar(werner);

                recognizer.SpeechRecognized += onSpeechRecognized;
                recognizer.SpeechDetected += onSpeechDetected;
                recognizer.AudioStateChanged += onAudioStateChange;

                recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(1.5);
                recognizer.BabbleTimeout = TimeSpan.FromSeconds(1.5);
                recognizer.EndSilenceTimeout = TimeSpan.FromSeconds(1.5);
                recognizer.EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(1.5);

                recognizer.SetInputToDefaultAudioDevice();
                Log.Info("Starting Speech Recognition Engine..");
                recognizer.RecognizeAsync(RecognizeMode.Multiple);
            }
        }

        private async void onSpeechDetected(object? sender, SpeechDetectedEventArgs e)
        {
            Log.Debug("detected speech.");
        }

        public bool IsTalking()
        {
            return CurrentAudioState == AudioState.Speech;
        }

        public bool IsSilent()
        {
            return !IsTalking();
        }

        private async void onAudioStateChange(object? sender, AudioStateChangedEventArgs e)
        {
            Log.Debug($"onAudioStateChange. {e.AudioState}");
            CurrentAudioState = e.AudioState;
        }

        public async void onSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            Log.Debug("recognized speech. ");
            Log.Debug($"Grammar: {e.Result.Grammar.Name}, Confidence: {e.Result.Confidence}");

            if (e.Result.Semantics.ContainsKey("command"))
            {
                Log.Debug($"Grammar: {e.Result.Grammar.Name}, Semantics: {e.Result.Semantics?["command"]?.Value}, Confidence: {e.Result.Confidence}");
            }

            if (e.Result.Confidence > 0.94f)
            {
                try
                {
                    //var command = e.Result.Text.Replace("hey", "").Replace("hellbot", "").Replace("madgod", "").Replace("sheogorath", "").Trim();
                    var command = e.Result.Semantics?["command"].Value.ToString();
                    Log.Info($"Confidence: {e.Result.Confidence}%, trying to {command} ({++Accepts} / {Accepts + Rejections})");
                    if (command == null)
                    {
                        Log.Debug("No matching command");
                    }
                    else if (command.Contains("say hello"))
                    {
                        await Server.Instance.chatgpt.GetResponse(Server.Instance.Assistant.Persona, "hello");
                    }
                    else if (command.Contains("what is this"))
                    {
                        if (e.Result.Grammar.Name == "Werner")
                        {
                            await Server.Instance.Narrator.ReactToCurrentScreen();

                        }
                        else
                        {
                            await Server.Instance.Assistant.ReactToCurrentScreen();
                        }
                    }
                    else if (command.Contains("roll dice"))
                    {
                        await Server.Instance.Assistant.RollDice();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"{ex.Message}");
                }
            }
            else
            {
                Log.Debug($"[{e.Result.Grammar.Name}] {e.Result.Text} (Confidence: {e.Result.Confidence})");
                //await Server.Instance.Assistant.RespondToPrompt(e.Result.Text);
                Rejections++;
            }
        }

        static Grammar CreateHellbotGrammar()
        {
            Choices hellbot = new Choices(new string[] { "hellbot", "sheogorath", "madgod" });
            Choices hellbotCommands = new Choices(new string[] { "what is this", "roll dice" });


            GrammarBuilder findServices = new GrammarBuilder("hey");
            findServices.Append(hellbot);
            findServices.Append(new SemanticResultKey("command", hellbotCommands));

            // Create a Grammar object from the GrammarBuilder. 
            Grammar servicesGrammar = new Grammar(findServices);
            servicesGrammar.Name = ("Sheogorath");
            return servicesGrammar;
        }

        static Grammar CreateWernerGrammar()
        {
            Choices hellbot = new Choices(new string[] { "werner", "verner" });
            Choices hellbotCommands = new Choices(new string[] { "what is this" });

            GrammarBuilder findServices = new GrammarBuilder("hey");
            findServices.Append(hellbot);
            findServices.Append(new SemanticResultKey("command", hellbotCommands));

            // Create a Grammar object from the GrammarBuilder. 
            Grammar servicesGrammar = new Grammar(findServices);
            servicesGrammar.Name = ("Werner");
            return servicesGrammar;
        }

        static DictationGrammar CreateDictationGrammar()
        {
            var grammar = new DictationGrammar();

            grammar.Name = "nothing";
            return grammar;
        }
    }
}
