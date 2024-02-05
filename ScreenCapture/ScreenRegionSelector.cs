using System.Drawing.Imaging;

namespace TwitchBot.ScreenCapture
{
    public partial class ScreenRegionSelector : Form
    {
        private PictureBox pictureBox;
        int mouseSelectX;
        int mouseSelectY;
        int selectWidth;
        int selectHeight;
        private List<Brush> selectBrushes = new()
        {
            Brushes.Yellow,
            Brushes.Red,
            Brushes.Blue
        };
        private Image screen;
        private int regionIndex;
        private List<Rectangle> regions;

        bool hasStartedDrawing = false;
        public ScreenRegionSelector(Image screen, int regionIndex, List<Rectangle> selectedRegions)
        {
            InitializeComponent();
            Top = 0;
            Left = 0;
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Black;
            Opacity = 0.2;
            ControlBox = false;
            pictureBox = new PictureBox();
            pictureBox.Size = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            pictureBox.Location = new Point(0, 0);
            pictureBox.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += PictureBox_MouseUp;
            pictureBox.BackColor = Color.Transparent;
            pictureBox.LoadCompleted += DrawScreenRegions;
            Controls.Add(pictureBox);
            this.screen = screen;
            this.regionIndex = regionIndex;
            this.regions = selectedRegions;
        }

        private void ScreenRegionSelector_Load(object sender, EventArgs e)
        {
            using (MemoryStream s = new MemoryStream())
            {
                screen.Save(s, ImageFormat.Bmp);
                pictureBox.Size = new Size(this.Width, this.Height);
            }
            Cursor = Cursors.Cross;
            DrawScreenRegions(sender, e);
        }

        private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            DrawScreenRegions(sender, e);

            if (hasStartedDrawing)
            {
                pictureBox.Refresh();
                DrawScreenRegions(sender, e);
                selectWidth = e.X - mouseSelectX;
                selectHeight = e.Y - mouseSelectY;
                pictureBox.CreateGraphics().FillRectangle(selectBrushes[regionIndex], mouseSelectX, mouseSelectY, selectWidth, selectHeight);
            }
        }

        private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (hasStartedDrawing)
            {
                if (e.Button == MouseButtons.Left)
                {
                    pictureBox.Refresh();
                    DrawScreenRegions(sender, e);
                    selectWidth = e.X - mouseSelectX;
                    selectHeight = e.Y - mouseSelectY;
                    pictureBox.CreateGraphics().FillRectangle(selectBrushes[regionIndex], mouseSelectX, mouseSelectY, selectWidth, selectHeight);

                }
                hasStartedDrawing = false;
                SaveScreenRegion();
            }
        }

        private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!hasStartedDrawing)
            {
                if (e.Button == MouseButtons.Left)
                {
                    mouseSelectX = e.X;
                    mouseSelectY = e.Y;
                }
                pictureBox.Refresh();
                DrawScreenRegions(sender, e);
                hasStartedDrawing = true;
            }
        }

        private void SaveScreenRegion()
        {
            if (selectWidth > 0)
            {
                Rectangle rect = new Rectangle(mouseSelectX, mouseSelectY, selectWidth, selectHeight);
                Server.Instance.screen.SetScreenRegion(rect, regionIndex);
            }
            this.Hide();
        }

        private void DrawScreenRegions(object? sender, EventArgs e)
        {
            for (int i = 0; i < regions.Count; i++) 
            { 
                if (i != regionIndex)
                {
                    var region = regions[i];
                    pictureBox.CreateGraphics().FillRectangle(selectBrushes[i], region.Left, region.Top, region.Width, region.Height);
                }
            }
        }
    }
}
