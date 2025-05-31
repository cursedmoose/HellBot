namespace TwitchBot.OBS
{
    public class LocalVocal
    {
        bool Enabled = false;

        string OutputPath = @"C:\Users\jimdu\Documents\Streaming\Transcriptions";
        string ArchivePath = @"C:\Users\jimdu\Documents\Streaming\Transcriptions\Archive\";
        string TranscriptPath = @"C:\Users\jimdu\Documents\Streaming\Transcriptions\desktop-output.srt";
        string CommentaryPath = @"C:\Users\jimdu\Documents\Streaming\Transcriptions\mic-output.srt";

        public LocalVocal(bool enabled)
        {
            Enabled = enabled;

        }

        public List<string> Archive()
        {
            return new()
            {
                ArchiveTranscript(),
                ArchiveCommentary()
            };
        }

        public string ArchiveTranscript()
        {
            return ArchiveFile(TranscriptPath);
        }

        public string ArchiveCommentary()
        {
            return ArchiveFile(CommentaryPath);
        }

        private string ArchiveFile(string filePath)
        {
            var currentArchivePath = Path.Combine(ArchivePath + $"{DateTime.Now:yyyy-MM-dd}");
            Directory.CreateDirectory(currentArchivePath);
            var numberOfFiles = Directory.GetFiles(currentArchivePath).Length;
            var newFileName = Path.GetFileNameWithoutExtension(filePath) + $"-{numberOfFiles}" + Path.GetExtension(filePath);
            var newFilePath = Path.Combine(currentArchivePath, newFileName);

            File.Move(filePath, newFilePath);

            return newFilePath;
        }
    }
}
