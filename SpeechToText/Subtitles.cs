using System.Text.RegularExpressions;

namespace TwitchBot.SpeechToText
{
    public class Subtitles
    {
        Logger Log = new("Subtitles");
        public readonly string FileName;
        private readonly string FilePath;
        public static readonly string SubtitlePath = @"subtitles/";
        string ArchivePath = Path.Combine(SubtitlePath, "Archive");


        public Subtitles(string fileName)
        {
            Directory.CreateDirectory(SubtitlePath);
            FileName = fileName;
            FilePath = Path.Combine(SubtitlePath, fileName);
        }

        public void Record(Subtitle subtitle)
        {
            var spaced_text = subtitle.ToString() + "\n\n";
            File.AppendAllTextAsync(FilePath, spaced_text);
        }

        public string ArchiveFiles()
        {
            var allFiles = Directory.GetFiles(SubtitlePath);
            var numberOfFiles = allFiles.Length;
            Log.Info($"Found {numberOfFiles} files to archive in {SubtitlePath}.");

            var currentArchivePath = Path.Combine(ArchivePath, $"{DateTime.Now:yyyy-MM-dd}");
            Directory.CreateDirectory(currentArchivePath);
            var numberOfArchives = Directory.GetFiles(currentArchivePath).Length;
            var archiveFileName = $"archive-{numberOfArchives}.vtt";
            var archiveFilePath = Path.Combine(currentArchivePath, archiveFileName);

            List<Subtitle> subtitles = new List<Subtitle>();
            foreach (var file in allFiles)
            {
                if (file.EndsWith(".srt"))
                {
                    Log.Info($"Archiving {file}");
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    subtitles.AddRange(FromSrtFile(file, fileName));
                    File.Delete(file);
                }
                else if (file.EndsWith(".vtt"))
                {
                    Log.Info($"Archiving {file}");
                    subtitles.AddRange(FromFile(file));
                    File.Delete(file);
                }
                else
                {
                    Log.Info($"Could not archive {file}: Unknown Extension");
                }
            }

            ToFile(subtitles, archiveFilePath);


            return archiveFilePath;
        }


        public List<Subtitle> FromSrtFile(string fileName, string speaker = "")
        {
            var fileText = File.ReadAllLines(fileName);
            var subtitles = new List<Subtitle>();

            if (fileText.Length % 3 != 0)
            {
                Console.WriteLine("Missing subtitle info?");
            }

            for (int i = 0; i < fileText.Length; i += 3)
            {
                var timeMatch = Regex.Match(fileText[i], @"(?<start>\d+) --> (?<end>\d+)");
                if (timeMatch.Success)
                {
                    var start = ulong.Parse(timeMatch.Groups["start"].Value) / 1000;
                    var end = ulong.Parse(timeMatch.Groups["end"].Value) / 1000;

                    var text = fileText[i + 1];

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
                    Console.WriteLine($"Could not parse timestamps from {fileName}:{i}");
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
                var timeMatch = Regex.Match(fileText[i], @"(?<start>\d+) --> (?<end>\d+)");
                var speakerMatch = Regex.Match(fileText[i + 1], @"<v (?<speaker>.*?)>(?<text>.*)");

                if (timeMatch.Success && speakerMatch.Success)
                {
                    var start = ulong.Parse(timeMatch.Groups["start"].Value);
                    var end = ulong.Parse(timeMatch.Groups["end"].Value);
                    var speaker = speakerMatch.Groups["speaker"].Value;
                    var text = speakerMatch.Groups["text"].Value;

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
                    Console.WriteLine($"Could not parse timestamps from {fileName}:{i}");
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
                file.WriteLine();
            }

            return filePath;
        }
    }

    public class Subtitle: IComparable<Subtitle>
    {
        public ulong StartTime;
        public ulong EndTime;
        public string Line = "";
        public string Speaker = "Unknown";

        public string TimeString()
        {
            return $"{StartTime} --> {EndTime}";
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
