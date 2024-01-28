using System.Drawing.Drawing2D;
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
        private Brush selectBrush;
        private Image screen;

        bool hasStartedDrawing = false;
        public ScreenRegionSelector(Image screen)
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
            selectBrush = Brushes.Yellow;
            Controls.Add(pictureBox);
            this.screen = screen;
        }

        private void ScreenRegionSelector_Load(object sender, EventArgs e)
        {
            using (MemoryStream s = new MemoryStream())
            {
                screen.Save(s, ImageFormat.Bmp);
                pictureBox.Size = new Size(this.Width, this.Height);
            }
            Cursor = Cursors.Cross;
        }

        private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (hasStartedDrawing)
            {
                pictureBox.Refresh();
                selectWidth = e.X - mouseSelectX;
                selectHeight = e.Y - mouseSelectY;
                pictureBox.CreateGraphics().FillRectangle(selectBrush, mouseSelectX, mouseSelectY, selectWidth, selectHeight);

            }
        }

        private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (hasStartedDrawing)
            {
                if (e.Button == MouseButtons.Left)
                {
                    pictureBox.Refresh();
                    selectWidth = e.X - mouseSelectX;
                    selectHeight = e.Y - mouseSelectY;
                    pictureBox.CreateGraphics().FillRectangle(selectBrush, mouseSelectX, mouseSelectY, selectWidth, selectHeight);

                }
                hasStartedDrawing = false;
                SaveToClipboard();
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
                hasStartedDrawing = true;
            }
        }

        private void SaveToClipboard()
        {
            if (selectWidth > 0)
            {
                Rectangle rect = new Rectangle(mouseSelectX, mouseSelectY, selectWidth, selectHeight);
                Server.Instance.screen.SetScreenRegion(rect);
            }
            this.Hide();
        }
    }
}
