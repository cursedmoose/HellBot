namespace TwitchBot.ScreenCapture
{
    internal class ClipboardScraper
    {
        readonly Logger log = new("ClipboardScraper");
        private Image previousImage;
        private bool Scraper_Running = false;

        internal ClipboardScraper(Image defaultImage)
        {
            previousImage = defaultImage;
        }
        internal bool HasNewImage()
        {
            bool hasNewImage = false;
            Thread clipboardThread = new Thread(
                delegate ()
                {
                    if (Clipboard.ContainsImage())
                    {
                        var clipboardImage = Clipboard.GetImage();
                        hasNewImage = true;
                    }
                });
            clipboardThread.SetApartmentState(ApartmentState.STA);
            clipboardThread.Start();
            clipboardThread.Join();
            return hasNewImage;
        }

        internal Image GetImage()
        {
            Image newImage = previousImage;
            Thread clipboardThread = new Thread(
                delegate ()
                {
                    if (Clipboard.ContainsImage())
                    {
                        var clipboardImage = Clipboard.GetImage();
                        Clipboard.Clear();
                        newImage = clipboardImage;
                        previousImage = clipboardImage;
                    }
                    else
                    {
                        log.Error("No image was found on the clipboard.");
                    }
                });
            clipboardThread.SetApartmentState(ApartmentState.STA);
            clipboardThread.Start();
            clipboardThread.Join();
            return newImage;
        }

        internal void ClearClipboard()
        {
            Thread clipboardThread = new Thread(
                delegate ()
                {
                    Clipboard.Clear();
                });
            clipboardThread.SetApartmentState(ApartmentState.STA);
            clipboardThread.Start();
            clipboardThread.Join();
        }
        private async Task Run()
        {
            ClearClipboard();
            Scraper_Running = true;
            do
            {
                await Scrape_Clipboard();
            } while (Scraper_Running);

            return;
        }

        private async Task Scrape_Clipboard()
        {
            if (HasNewImage())
            {
                var filePath = "images/screenshots/clipboard.png";
                var image = GetImage();
                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    image.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
                }
                await Server.Instance.Narrator.ReadImage(ImageFiles.Clipboard);
            }
            await Task.Delay(1_000);
        }
        internal async Task Start()
        {
            if (!Scraper_Running)
            {
                log.Info($"Hello at {DateTime.Now}");
                Scraper_Running = true;
                await Task.Run(Run);
            }
            return;
        }

        internal bool IsRunning()
        {
            return Scraper_Running;
        }

        internal async Task Stop()
        {
            log.Info($"Goodbye at {DateTime.Now}");
            Scraper_Running = false;
            return;
        }

        private List<bool> GetHash(Image bmpSource)
        {
            List<bool> lResult = new List<bool>();
            //create new image with 16x16 pixel
            Bitmap bmpMin = new Bitmap(bmpSource, new Size(16, 16));
            for (int j = 0; j < bmpMin.Height; j++)
            {
                for (int i = 0; i < bmpMin.Width; i++)
                {
                    //reduce colors to true / false                
                    lResult.Add(bmpMin.GetPixel(i, j).GetBrightness() < 0.5f);
                }
            }
            return lResult;
        }
    }
}
