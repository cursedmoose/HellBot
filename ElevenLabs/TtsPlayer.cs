﻿using NAudio.Wave;
using System.Diagnostics;
using System.Media;

namespace TwitchBot.ElevenLabs
{
    internal class TtsPlayer
    {
        static int counter;
        static readonly Logger Log = new("PlayTTS");

        public static void PlayResponseStream(Stream responseStream)
        {
            int messageId = counter++;
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                using (Stream ms = new MemoryStream())
                {
  
                    byte[] buffer = new byte[32768];
                    int read;
                    while ((read = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }

                    ms.Position = 0;

                    using Mp3FileReader reader = new(ms);
                    using WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
                    using Stream outStream = new MemoryStream();

                    WaveFileWriter.WriteWavFileToStream(outStream, pcmStream);
                    SoundPlayer soundPlayer = new(outStream);
                    if (soundPlayer.Stream != null)
                    {
                        soundPlayer.Stream.Position = 0;
                    }
                    timer.Stop();
                    Log.Debug($"[MSG-{messageId}] mp3 decode: {timer.ElapsedMilliseconds}ms");
                    timer.Restart();
                    soundPlayer.PlaySync();
                    timer.Stop();
                    Log.Info($"[MSG-{messageId}] mp3 length: {timer.ElapsedMilliseconds}ms");

                }
            }
            catch (Exception e)
            {
                Log.Error($"[MSG-{messageId}] Caught exception trying to decode+play audio stream: {e.Message}");
            }
        }
    }
}
