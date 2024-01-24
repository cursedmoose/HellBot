namespace TwitchBot.ScreenCapture
{
    public class ScreenCapturer
    {
        readonly Logger log = new("ScreenCapturer");
        private ClipboardScraper scraper;

        public ScreenCapturer()
        {
            scraper = new(CaptureScreen());
        }

        public Bitmap CaptureScreen()
        {
            Bitmap bmp = new(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
            }
            return bmp;
        }

        public string TakeScreenshot()
        {
            var filePath = "images/screenshots/latest.png";
            var img = CaptureScreen();
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                img.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
            }
            return filePath;
        }

        public bool ClipboardHasNewImage()
        {
            return scraper.HasNewImage();
        }

        public Image GetClipboardImage()
        {
            return scraper.GetImage();
        }

        public async Task StartScraper()
        {
            if (!scraper.IsRunning())
            {
                log.Info($"Scraper started at {DateTime.Now}");
                await Task.Run(scraper.Start);
            }
            return;
        }

        public async Task StopScraper()
        {
            log.Info($"Goodbye at {DateTime.Now}");
            await scraper.Stop();
            return;
        }
    }
}
