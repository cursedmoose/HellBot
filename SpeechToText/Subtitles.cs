using System.Text.RegularExpressions;
using Amazon.S3.Model;

namespace TwitchBot.SpeechToText
{
    public class Subtitles
    {
        Logger Log = new("Subtitles");
        public Subtitles()
        {

        }


        public List<Subtitle> FromSrtFile(string fileName, string speaker = "")
        {
            var fileText = File.ReadAllLines(fileName);
            var subtitles = new List<Subtitle>();

            if (fileText.Length % 4 != 0)
            {
                Log.Error("Missing subtitle info?");
            }

            for (int i = 0; i < fileText.Length; i += 4)
            {
                var subtitleIndex = fileText[i];
                var timeMatch = Regex.Match(fileText[i + 1], @"(?<start>\d{2}:\d{2}:\d{2},\d{3}) --> (?<end>\d{2}:\d{2}:\d{2},\d{3})");
                if (timeMatch.Success)
                {
                    var start = TimeSpan.ParseExact(timeMatch.Groups["start"].Value, @"hh\:mm\:ss\,fff", null);
                    var end = TimeSpan.ParseExact(timeMatch.Groups["end"].Value, @"hh\:mm\:ss\,fff", null);

                    var text = fileText[i + 2];

                    var subtitle = new Subtitle()
                    {
                        StartTime = start,
                        EndTime = end,
                        Line = text,
                        Speaker = speaker
                    };

                    subtitles.Add(subtitle);
                }
                else
                {
                    Log.Error($"Could not parse timestamps from {fileName}:{i}");
                }                    
            }


            return subtitles;
        }

        public List<Subtitle> FromFile(string fileName)
        {
            var fileText = File.ReadAllLines(fileName);
            var subtitles = new List<Subtitle>();

            if (fileText.Length % 3 != 0)
            {
                Log.Error("Missing subtitle info?");
            }

            for (int i = 0; i < fileText.Length; i += 3)
            {
                var timeMatch = Regex.Match(fileText[i + 1], @"(?<start>\d{2}:\d{2}:\d{2},\d{3}) --> (?<end>\d{2}:\d{2}:\d{2},\d{3})");
                var speakerMatch = Regex.Match(fileText[i + 2], @"(?<speaker><v.*?>)(?<dialog>)");

                if (timeMatch.Success && speakerMatch.Success)
                {
                    var start = TimeSpan.ParseExact(timeMatch.Groups["start"].Value, @"hh\:mm\:ss\.fff", null);
                    var end = TimeSpan.ParseExact(timeMatch.Groups["end"].Value, @"hh\:mm\:ss\.fff", null);
                    var text = speakerMatch.Groups["speaker"].Value;
                    var speaker = speakerMatch.Groups["dialog"].Value;

                    var subtitle = new Subtitle()
                    {
                        StartTime = start,
                        EndTime = end,
                        Line = text,
                        Speaker = speaker
                    };

                    subtitles.Add(subtitle);
                }
                else
                {
                    Log.Error($"Could not parse timestamps from {fileName}:{i}");
                }
            }


            return subtitles;
        }

        public string ToFile(Subtitle subtitle, string filePath)
        {
            return ToFile(new List<Subtitle>{ subtitle }, filePath);
        }

        public string ToFile(List<Subtitle> subtitles, string filePath)
        {
            using var file = new StreamWriter(filePath);
            subtitles.Sort();

            foreach (var subtitle in subtitles)
            {
                file.WriteLine(subtitle);
            }

            return filePath;
        }
    }

    public class Subtitle: IComparable<Subtitle>
    {
        public TimeSpan StartTime;
        public TimeSpan EndTime;
        public string Line = "";
        public string Speaker = "Unknown";

        public string TimeString()
        {
            return $"{StartTime.ToString(@"hh\:mm\:ss\.fff")} --> {EndTime.ToString(@"hh\:mm\:ss\.fff")}";
        }

        public string DialogString()
        {
            return $"<v {Speaker}>{Line}";
        }

        public override string ToString()
        {
            return string.Join("\n", TimeString(), DialogString());
        }

        int IComparable<Subtitle>.CompareTo(Subtitle? other)
        {
            return StartTime.CompareTo(other?.StartTime);
        }
    }
}
