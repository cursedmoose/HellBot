using System.Drawing.Imaging;

namespace TwitchBot.ScreenCapture
{
    public class ScreenCapturer
    {
        readonly Logger log = new("ScreenCapturer");
        private ClipboardScraper scraper;
        private List<Rectangle> screenRegions = new()
        {
            new(0, 0, 0, 0),
            new(0, 0, 0, 0),
            new(0, 0, 0, 0)
        };

        public int SelectedRegionArea(int regionIndex)
        {
            return screenRegions[regionIndex].Height * screenRegions[regionIndex].Width;
        }

        public ScreenCapturer()
        {
            scraper = new(CaptureScreen());
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

        private Bitmap CaptureScreenRegion(int regionIndex)
        {
            Bitmap bmp = new(screenRegions[regionIndex].Width, screenRegions[regionIndex].Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenRegions[regionIndex].Left, screenRegions[regionIndex].Top, 0, 0, screenRegions[regionIndex].Size);
            }
            return bmp;
        }

        public string TakeScreenshot()
        {
            var filePath = "images/screenshots/latest.png";
            var img = CaptureScreen();
            // If this is erroring, you're probably calling it twice.
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                img.Save(fs, ImageFormat.Png);
            }
            return filePath;
        }

        public string TakeScreenRegion(int regionIndex)
        {
            var filePath = "images/screenshots/region.png";
            var img = CaptureScreenRegion(regionIndex);
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                img.Save(fs, ImageFormat.Png);
            }
            return filePath;
        }

        public Bitmap GetScreenRegion(int regionIndex)
        {
            var img = CaptureScreenRegion(regionIndex);
            return img;
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

        public void SelectScreenRegion(int regionIndex)
        {
            Task mytask = Task.Run(() =>
            {
                var form = new ScreenRegionSelector(CaptureScreen(), regionIndex, screenRegions);
                form.ShowDialog();
            });
        }

        public void SetScreenRegion(Rectangle rect, int regionIndex = 0)
        {
            screenRegions[regionIndex] = rect;
            log.Info($"Set selectedRegion[{regionIndex}] to {rect.Location}{rect.Size.Width}x{rect.Size.Height}");
        }
    }
}
