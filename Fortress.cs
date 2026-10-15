using System;
using System.Diagnostics;
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
        float WorldWidth { get { return ClientSize.Width / GameScale; } }
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random random = new Random();
        readonly Font normal = new Font("Segoe UI", 10, FontStyle.Bold);
        readonly Font small = new Font("Segoe UI", 9);
        readonly Font resultFont = new Font("Segoe UI", 22, FontStyle.Bold);
        readonly Color ink = Color.FromArgb(22, 30, 45);
        readonly Color green = Color.FromArgb(102, 235, 177);
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
            float left = Math.Min(390, WorldWidth * 0.48f);
            float right = Math.Max(left + 1, WorldWidth - 50);
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
            double range = 70 + charge * Math.Max(100, WorldWidth - muzzle.X - 95);
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush dark = new SolidBrush(ink)) g.FillRectangle(dark, panel);
            DrawText(g, String.Format("{0} HIT / {1} SHOTS", hits, shots), small, focused ? green : Color.LightGray, panel.X + 6, panel.Y + 3);
            DrawText(g, "×", normal, Color.LightGray, panel.Right - 20, panel.Y + 1);

            // A narrow power column sits beside the tank, filling from the ground up.
            Rectangle meter = new Rectangle(100, (int)ground - 44, 13, 42);
            using (SolidBrush b = new SolidBrush(ink)) g.FillRectangle(b, meter.X - 3, meter.Y - 3, meter.Width + 6, meter.Height + 6);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(53, 67, 86))) g.FillRectangle(b, meter);
            float filledHeight = (float)(meter.Height * charge);
            using (SolidBrush b = new SolidBrush(charge > 0.85 ? Color.FromArgb(255, 195, 98) : green))
                g.FillRectangle(b, meter.X, meter.Bottom - filledHeight, meter.Width, filledHeight);

            string hint = !focused ? "Click tank to play" : charging ? ((int)(charge * 100)) + "%" : flying || result.Length > 0 ? "" : "Hold SPACE";
            if (hint.Length > 0)
            {
                SizeF hintSize = g.MeasureString(hint, small);
                using (SolidBrush b = new SolidBrush(ink)) g.FillRectangle(b, panel.X, ground - 47, hintSize.Width + 12, 21);
                DrawText(g, hint, small, Color.FromArgb(207, 217, 232), panel.X + 6, ground - 45);
            }

            // Tank stands directly on the top edge of the taskbar.
            using (Pen barrel = new Pen(ink, 13)) g.DrawLine(barrel, 58, ground - 18, muzzle.X, muzzle.Y);
            using (Pen barrel = new Pen(green, 7)) g.DrawLine(barrel, 58, ground - 18, muzzle.X, muzzle.Y);
            using (SolidBrush b = new SolidBrush(ink)) g.FillEllipse(b, 23, ground - 21, 64, 21);
            using (SolidBrush b = new SolidBrush(green)) { g.FillRectangle(b, 28, ground - 28, 49, 15); g.FillEllipse(b, 43, ground - 36, 26, 20); }
            for (int x = 33; x < 83; x += 13)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(126, 146, 166))) g.FillEllipse(b, x, ground - 14, 9, 9);

            using (SolidBrush b = new SolidBrush(ink)) g.FillRectangle(b, targetX - 23, ground - 5, 46, 5);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 112, 119))) g.FillEllipse(b, targetX - 19, ground - 38, 38, 38);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 242, 216))) g.FillEllipse(b, targetX - 12, ground - 31, 24, 24);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(238, 82, 99))) g.FillEllipse(b, targetX - 5, ground - 24, 10, 10);
            if (flying)
            {
                for (int i = 5; i >= 0; i--)
                {
                    double t = Math.Max(0, flightTime - i * 0.025);
                    PointF p = Ballistics.Position(muzzle, speed, t);
                    float radius = i == 0 ? 5 : 2;
                    using (SolidBrush b = new SolidBrush(i == 0 ? Color.FromArgb(255, 218, 123) : Color.FromArgb(154, 171, 190)))
                        g.FillEllipse(b, p.X - radius, p.Y - radius, radius * 2, radius * 2);
                }
            }
            if (result.Length > 0)
            {
                SizeF size = g.MeasureString(result, resultFont);
                float x = targetX - size.Width / 2, y = ground - 88;
                using (SolidBrush b = new SolidBrush(ink)) g.FillRectangle(b, x - 6, y, size.Width + 12, size.Height);
                DrawText(g, result, resultFont, wasHit ? green : Color.FromArgb(255, 135, 141), x, y);
            }
        }

        static void DrawText(Graphics g, string text, Font font, Color color, float x, float y)
        {
            using (SolidBrush brush = new SolidBrush(color)) g.DrawString(text, font, brush, x, y);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); normal.Dispose(); small.Dispose(); resultFont.Dispose(); }
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
