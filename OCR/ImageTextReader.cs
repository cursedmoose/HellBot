namespace TwitchBot.OCR
{
    public interface ImageTextReader
    {
        Task<string> ReadText(Bitmap image);
        Task<string> ReadText(string filePath = "images/screenshots/screenreader.png");
    }
}
