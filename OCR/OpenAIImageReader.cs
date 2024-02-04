using System.Drawing.Imaging;

namespace TwitchBot.OCR
{
    public class OpenAIImageReader : ImageTextReader
    {
        ChatGpt.ChatGpt gpt;
        private readonly Logger log = new("OpenAI-Vision");
        public OpenAIImageReader(ChatGpt.ChatGpt chatgpt)
        {
            gpt = chatgpt;
        }
        public async Task<string> ReadText(Bitmap image)
        {
            log.Debug($"Reading text from Image...");
            var imageFile = SaveImageLocally(image);
            return await ReadText(imageFile);
        }

        private string SaveImageLocally(Bitmap image)
        {
            var filePath = "images/screenshots/screenreader/chatgpt.png";
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                image.Save(fs, ImageFormat.Png);
            }
            return filePath;
        }

        public async Task<string> ReadText(string filePath = "images/screenshots/screenreader.png")
        {
            var imageUrl = await Server.Instance.UploadImage(filePath);
            var text = await gpt.ExtractTextFromImage(imageUrl);
            return text;
        }
    }
}
