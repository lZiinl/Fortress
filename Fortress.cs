using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TaskbarFortress
{
    public static class Ballistics
    {
        public const double Gravity = 620;
        public const double Angle = Math.PI / 4;
        public static double SpeedForRange(double range, double height)
        {
            return Math.Sqrt(Gravity * range * range / (2 * Math.Cos(Angle) * Math.Cos(Angle) * (height + range * Math.Tan(Angle))));
        }
        public static PointF Position(PointF start, double speed, double time)
        {
            return new PointF((float)(start.X + speed * Math.Cos(Angle) * time),
                (float)(start.Y - speed * Math.Sin(Angle) * time + Gravity * time * time / 2));
        }
    }

    public sealed class Game : Form
    {
        // Rendering, mouse input and physics share compact logical coordinates.
        const float GameScale = 0.60f;
        const float TankOriginX = 55; // Center of the tank tracks is position 0.
        float WorldWidth { get { return ClientSize.Width / GameScale; } }
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random random = new Random();
        readonly Color ink = Color.FromArgb(22, 30, 45);
        readonly Color green = Color.FromArgb(102, 235, 177);
        static readonly string[] TankPixels = {
            "................DD..",
            "...............DLD..",
            "..............DLD...",
            ".............DLD....",
            ".......DDDD.DLD.....",
            "......DLLLLDLD......",
            "......DGGGGGD.......",
            "...DDDDGGGGGDDDDD...",
            "..DLLLLLLLLLLLLLGD..",
            "..DGGGGGGGGGGGGGSD..",
            ".DDSSSSSSSSSSSSSSDD.",
            "DWWDWWDWWDWWDWWDWWDD",
            ".DDDDDDDDDDDDDDDDDD."
        };
        static readonly string[] TargetPixels = {
            "....DDDD....",
            "..DDRRRRDD..",
            ".DRRCCCCRRD.",
            ".DRCCRRCCRD.",
            "DRCCRBBRCCRD",
            "DRCRBBBBRCRD",
            "DRCRBBBBRCRD",
            "DRCCRBBRCCRD",
            ".DRCCRRCCRD.",
            ".DRRCCCCRRD.",
            "..DDRRRRDD..",
            "DDDDDDDDDDDD"
        };
        Rectangle panel;
        PointF muzzle, projectile;
        float ground, targetX;
        double lastTime, flightTime, speed, charge, resultUntil;
        bool charging, flying, focused, wasHit;
        string result = "";
        int hits, shots;

        public Game()
        {
            Text = "Taskbar Fortress";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            TopMost = true;
            ShowInTaskbar = true;
            DoubleBuffered = true;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;
            ResetLayout();
            Activated += delegate { focused = true; Invalidate(); };
            Deactivate += delegate { focused = false; charging = false; charge = 0; Invalidate(); };
            MouseDown += OnMouse;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            timer.Interval = 16;
            timer.Tick += Tick;
            timer.Start();
        }

        void ResetLayout()
        {
            Bounds = Screen.PrimaryScreen.WorkingArea;
            ground = (ClientSize.Height - 2) / GameScale;
            muzzle = new PointF(80, ground - 39);
            panel = new Rectangle(126, (int)ground - 24, 200, 24);
            charging = flying = false;
            charge = 0;
            result = "";
            NewTarget();
        }

        void NewTarget()
        {
            float span = WorldWidth - TankOriginX;
            float left = TankOriginX + span * 0.10f;
            float right = TankOriginX + span * 0.80f;
            targetX = left + (float)random.NextDouble() * (right - left);
        }

        void OnMouse(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            PointF worldMouse = new PointF(e.X / GameScale, e.Y / GameScale);
            if (new RectangleF(panel.Right - 24, panel.Top, 24, panel.Height).Contains(worldMouse)) { Close(); return; }
            Activate();
            Focus();
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); return; }
            if (e.KeyCode != Keys.Space) return;
            e.Handled = e.SuppressKeyPress = true;
            if (!charging && !flying && result.Length == 0) { charging = true; charge = 0; }
        }

        void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Space) return;
            e.Handled = e.SuppressKeyPress = true;
            if (!charging) return;
            charging = false;
            double maxRange = WorldWidth - muzzle.X;
            double minRange = Math.Min(70, maxRange);
            double range = minRange + charge * (maxRange - minRange);
            speed = Ballistics.SpeedForRange(range, ground - muzzle.Y);
            flightTime = 0;
            projectile = muzzle;
            flying = true;
            shots++;
        }

        void Tick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.05, now - lastTime);
            lastTime = now;
            if (Bounds != Screen.PrimaryScreen.WorkingArea) ResetLayout();
            if (charging) charge = Math.Min(1, charge + dt / 1.5);
            if (flying)
            {
                // Small physics steps prevent a fast shell from skipping the target.
                int steps = Math.Max(1, (int)Math.Ceiling(dt / 0.004));
                for (int i = 0; i < steps && flying; i++)
                {
                    flightTime += dt / steps;
                    projectile = Ballistics.Position(muzzle, speed, flightTime);
                    double dx = projectile.X - targetX, dy = projectile.Y - (ground - 19);
                    if (dx * dx + dy * dy <= 23 * 23) Finish(true, now);
                    else if (projectile.Y >= ground || projectile.X > WorldWidth + 10) Finish(false, now);
                }
            }
            if (result.Length > 0 && now >= resultUntil)
            {
                if (wasHit) NewTarget();
                result = "";
                charge = 0;
            }
            Invalidate();
        }

        void Finish(bool hit, double now)
        {
            flying = false;
            wasHit = hit;
            if (hit) hits++;
            result = hit ? "HIT" : "MISS";
            resultUntil = now + 1.2;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.ScaleTransform(GameScale, GameScale);
            g.SmoothingMode = SmoothingMode.None;
            string score = String.Format("{0} HIT / {1} SHOTS", hits, shots);
            panel.Width = (int)Math.Ceiling(TextWidth(score, 1)) + 36;
            PixelRect(g, ink, panel.X, panel.Y, panel.Width, panel.Height);
            DrawPixelText(g, score, 1, focused ? green : Color.LightGray, panel.X + 6, panel.Y + 6);
            PixelRect(g, Color.FromArgb(53, 67, 86), panel.Right - 24, panel.Y + 3, 2, panel.Height - 6);
            DrawPixelText(g, "X", 1, Color.LightGray, panel.Right - 16, panel.Y + 6);

            // A narrow power column sits beside the tank, filling from the ground up.
            Rectangle meter = new Rectangle(100, (int)ground - 44, 13, 42);
            PixelRect(g, ink, meter.X - 3, meter.Y - 3, meter.Width + 6, meter.Height + 6);
            int lit = (int)Math.Ceiling(charge * 8);
            for (int i = 0; i < 8; i++)
            {
                Color segment = i < lit ? (i >= 6 ? Color.FromArgb(255, 195, 98) : green) : Color.FromArgb(53, 67, 86);
                PixelRect(g, segment, meter.X, meter.Bottom - (i + 1) * 5, meter.Width, 3);
            }

            string hint = !focused ? "CLICK TANK" : charging ? ((int)(charge * 100)) + "%" : flying || result.Length > 0 ? "" : "HOLD SPACE";
            if (hint.Length > 0)
            {
                PixelRect(g, ink, panel.X, ground - 47, TextWidth(hint, 1) + 12, 21);
                DrawPixelText(g, hint, 1, Color.FromArgb(207, 217, 232), panel.X + 6, ground - 42);
            }

            DrawPixelSprite(g, TankPixels, TankOriginX, ground);
            DrawPixelSprite(g, TargetPixels, targetX, ground);
            if (flying)
            {
                for (int i = 5; i >= 0; i--)
                {
                    double t = Math.Max(0, flightTime - i * 0.025);
                    PointF p = Ballistics.Position(muzzle, speed, t);
                    float radius = i == 0 ? 5 : 2;
                    PixelRect(g, i == 0 ? ink : Color.FromArgb(154, 171, 190), p.X - radius, p.Y - radius, radius * 2, radius * 2);
                    if (i == 0) PixelRect(g, Color.FromArgb(255, 218, 123), p.X - 3, p.Y - 3, 6, 6);
                }
            }
            if (result.Length > 0)
            {
                SizeF size = new SizeF(TextWidth(result, 2), 14 / GameScale);
                float x = targetX + 31;
                if (x + size.Width + 6 > WorldWidth)
                    x = targetX - 31 - size.Width;
                float y = ground - 19 - size.Height / 2;
                PixelRect(g, ink, x - 6, y - 4, size.Width + 12, size.Height + 8);
                DrawPixelText(g, result, 2, wasHit ? green : Color.FromArgb(255, 135, 141), x, y);
            }
        }

        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string> {
            {'0', "0E11131519110E"}, {'1', "040C040404040E"},
            {'2', "0E11010204081F"}, {'3', "1E01010E01011E"},
            {'4', "02060A121F0202"}, {'5', "1F10101E01011E"},
            {'6', "0E10101E11110E"}, {'7', "1F010204080808"},
            {'8', "0E11110E11110E"}, {'9', "0E11110F01010E"},
            {'A', "0E11111F111111"}, {'C', "0E11101010110E"},
            {'D', "1E11111111111E"}, {'E', "1F10101E10101F"},
            {'H', "1111111F111111"}, {'I', "0E04040404040E"},
            {'K', "11121418141211"}, {'L', "1010101010101F"},
            {'M', "111B1515111111"}, {'N', "11191915131311"},
            {'O', "0E11111111110E"}, {'P', "1E11111E101010"},
            {'S', "0F10100E01011E"}, {'T', "1F040404040404"},
            {'X', "11110A040A1111"}, {'/', "01010204081010"},
            {'%', "191A0204080B13"}
        };

        static float TextWidth(string text, int pixel)
        {
            return Math.Max(0, text.Length * 6 - 1) * pixel / GameScale;
        }

        static void PixelRect(Graphics g, Color color, float x, float y, float width, float height)
        {
            GraphicsState state = g.Save();
            g.ResetTransform();
            using (SolidBrush brush = new SolidBrush(color))
                g.FillRectangle(brush, (int)Math.Round(x * GameScale), (int)Math.Round(y * GameScale),
                    Math.Max(1, (int)Math.Round(width * GameScale)), Math.Max(1, (int)Math.Round(height * GameScale)));
            g.Restore(state);
        }

        static void DrawPixelText(Graphics g, string text, int pixel, Color color, float x, float y)
        {
            GraphicsState state = g.Save();
            g.ResetTransform();
            int left = (int)Math.Round(x * GameScale), top = (int)Math.Round(y * GameScale);
            using (SolidBrush brush = new SolidBrush(color))
                foreach (char letter in text)
                {
                    string glyph;
                    if (Glyphs.TryGetValue(letter, out glyph))
                        for (int row = 0; row < 7; row++)
                        {
                            int bits = Convert.ToInt32(glyph.Substring(row * 2, 2), 16);
                            for (int col = 0; col < 5; col++)
                                if ((bits & (1 << (4 - col))) != 0)
                                    g.FillRectangle(brush, left + col * pixel, top + row * pixel, pixel, pixel);
                        }
                    left += 6 * pixel;
                }
            g.Restore(state);
        }

        static void DrawPixelSprite(Graphics g, string[] rows, float centerX, float bottom)
        {
            // Draw on whole device pixels to avoid blurred edges and transparency-key halos.
            GraphicsState state = g.Save();
            g.ResetTransform();
            g.SmoothingMode = SmoothingMode.None;
            const int pixel = 2;
            int left = (int)Math.Round(centerX * GameScale) - rows[0].Length * pixel / 2;
            int top = (int)Math.Round(bottom * GameScale) - rows.Length * pixel;
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    Color color;
                    switch (rows[y][x])
                    {
                        case 'D': color = Color.FromArgb(25, 32, 45); break;
                        case 'L': color = Color.FromArgb(184, 246, 153); break;
                        case 'G': color = Color.FromArgb(93, 184, 106); break;
                        case 'S': color = Color.FromArgb(48, 105, 79); break;
                        case 'W': color = Color.FromArgb(137, 156, 171); break;
                        case 'R': color = Color.FromArgb(238, 91, 103); break;
                        case 'C': color = Color.FromArgb(255, 236, 192); break;
                        case 'B': color = Color.FromArgb(166, 43, 66); break;
                        default: continue;
                    }
                    using (SolidBrush brush = new SolidBrush(color))
                        g.FillRectangle(brush, left + x * pixel, top + y * pixel, pixel, pixel);
                }
            g.Restore(state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Game());
        }
    }
}
