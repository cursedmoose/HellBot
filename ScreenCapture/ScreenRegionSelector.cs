using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TwitchBot.ScreenCapture
{
    public partial class ScreenRegionSelector : Form
    {
        private PictureBox pictureBox;
        //These variables control the mouse position
        int selectX;
        int selectY;
        int selectWidth;
        int selectHeight;
        private Pen selectPen;
        private Brush selectBrush;
        private Image screen;

        //This variable control when you start the right click
        bool start = false;
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
            //TransparencyKey = Color.Black;
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
            selectPen = new Pen(Color.Red, 5);
            selectBrush = Brushes.Yellow;
            Controls.Add(pictureBox);
            this.screen = screen;
        }

        private void ScreenRegionSelector_Load(object sender, EventArgs e)
        {
            //this.Hide();

            //Create a temporal memory stream for the image
            using (MemoryStream s = new MemoryStream())
            {
                //save graphic variable into memory
                screen.Save(s, ImageFormat.Bmp);
                pictureBox.Size = new System.Drawing.Size(this.Width, this.Height);
                //set the picture box with temporary stream
                //pictureBox.Image = Image.FromStream(s);
            }
            //Show Form
            //this.Show();
            //Cross Cursor
            Cursor = Cursors.Cross;
        }

        private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            //validate if there is an image
            //if (pictureBox.Image == null)
            //    return;
            //validate if right-click was trigger
            if (start)
            {
                //refresh picture box
                pictureBox.Refresh();
                //set corner square to mouse coordinates
                selectWidth = e.X - selectX;
                selectHeight = e.Y - selectY;
                //draw dotted rectangle
                //pictureBox.CreateGraphics().DrawRectangle(selectPen, selectX, selectY, selectWidth, selectHeight);
                pictureBox.CreateGraphics().FillRectangle(selectBrush, selectX, selectY, selectWidth, selectHeight);

            }
        }

        private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (start)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    pictureBox.Refresh();
                    selectWidth = e.X - selectX;
                    selectHeight = e.Y - selectY;
                    //pictureBox.CreateGraphics().DrawRectangle(selectPen, selectX, selectY, selectWidth, selectHeight);
                    pictureBox.CreateGraphics().FillRectangle(selectBrush, selectX, selectY, selectWidth, selectHeight);

                }
                start = false;

                //function save image to clipboard
                SaveToClipboard();
            }
        }

        private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            //validate when user right-click
            if (!start)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    //starts coordinates for rectangle
                    selectX = e.X;
                    selectY = e.Y;
                    //selectPen = new Pen(Color.Red, 1);
                    selectPen.DashStyle = DashStyle.Solid;
                }
                //refresh picture box
                pictureBox.Refresh();
                //start control variable for draw rectangle
                start = true;
            }
            else
            {
            }
        }

        private void SaveToClipboard()
        {
            //validate if something selected
            if (selectWidth > 0)
            {
                Rectangle rect = new Rectangle(selectX, selectY, selectWidth, selectHeight);
                Server.Instance.screen.SetScreenRegion(rect);
            }
            //End application
            //Application.Exit();
            this.Hide();
        }
    }
}
