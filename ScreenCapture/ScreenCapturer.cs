using System.Drawing.Imaging;
using System.Windows.Forms;

namespace TwitchBot.ScreenCapture
{
    public class ScreenCapturer
    {
        readonly Logger log = new("ScreenCapturer");
        private ClipboardScraper scraper;
        private ScreenRegionSelector selectorForm;
        private Rectangle selectedRegion; // = new Rectangle(0, 0, 75, 200);

        public ScreenCapturer()
        {
            scraper = new(CaptureScreen());
            selectorForm = new ScreenRegionSelector(CaptureScreen());
        }

        private Bitmap CaptureScreen()
        {
            Bitmap bmp = new(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
            }
            return bmp;
        }

        private Bitmap CaptureScreenRegion()
        {
            Bitmap bmp = new(selectedRegion.Width, selectedRegion.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(selectedRegion.Left, selectedRegion.Top, 0, 0, selectedRegion.Size);
            }
            return bmp;
        }

        public string TakeScreenshot()
        {
            var filePath = "images/screenshots/latest.png";
            var img = CaptureScreen();
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                img.Save(fs, ImageFormat.Png);
            }
            return filePath;
        }

        public string TakeScreenRegion()
        {
            var filePath = "images/screenshots/region.png";
            var img = CaptureScreenRegion();
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                img.Save(fs, ImageFormat.Png);
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

        public void SelectScreenRegion()
        {
            Task mytask = Task.Run(() =>
            {
                selectorForm = new ScreenRegionSelector(CaptureScreen());
                selectorForm.ShowDialog();
            });
            var screen = CaptureScreen();
            var graphics = Graphics.FromImage(screen);
            graphics.CopyFromScreen(0, 0, 0, 0, screen.Size);

            using (MemoryStream s = new MemoryStream())
            {
                screen.Save(s, ImageFormat.Bmp);
            }
        }

        public void SetScreenRegion(Rectangle rect)
        {
            selectedRegion = rect;
            log.Info($"Set selectedRegion to {rect.Location}{rect.Size.Width}x{rect.Size.Height}");
        }
    }
}
