using Tesseract;

namespace TwitchBot.OCR
{
    public class TesseractImageReader : ImageTextReader
    {
        TesseractEngine tesseract;
        private readonly Logger log = new("Tesseract");

        public TesseractImageReader()
        {
            tesseract = new(@"./OCR/tessdata", "eng", EngineMode.LstmOnly);
        }
        public async Task<string> ReadText(Bitmap image)
        {
            return await Task.Run(() =>
            {
                log.Debug($"Reading text from Image...");
                using (var page = tesseract.Process(image))
                {
                    return page.GetText();
                }
            }
            );
        }

        public async Task<string> ReadText(string filePath = "images/screenshots/screenreader.png")
        {
            return await Task.Run(() =>
            {
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    var bitmap = new Bitmap(fs);
                    using (var page = tesseract.Process(bitmap))
                    {
                        log.Debug($"Reading text from {filePath}...");
                        return page.GetText();
                    }
                }
            });
        }
    }
}
