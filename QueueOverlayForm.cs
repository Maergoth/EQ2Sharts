using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HeartsActPlugin
{
    public sealed class QueueOverlayForm : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        private readonly Label LblQueue;
        private bool ClickThrough;
        private const float BaseFontSize = 10f;

        public QueueOverlayForm()
        {
            Text = "Hearts Queue";
            Size = new Size(200, 300);
            MinimumSize = new Size(120, 100);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 220, 100);
            BackColor = Color.FromArgb(30, 30, 30);

            LblQueue = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", BaseFontSize),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                AutoSize = false,
                Padding = new Padding(4)
            };

            Controls.Add(LblQueue);
        }

        public void SetClickThrough(bool enabled)
        {
            if (ClickThrough == enabled)
                return;

            ClickThrough = enabled;

            if (enabled)
            {
                FormBorderStyle = FormBorderStyle.None;

                if (IsHandleCreated)
                {
                    var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
                    SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                }
            }
            else
            {
                if (IsHandleCreated)
                {
                    var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
                    SetWindowLong(Handle, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
                }

                FormBorderStyle = FormBorderStyle.SizableToolWindow;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;

                if (ClickThrough)
                    cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;

                return cp;
            }
        }

        public void SetFontScale(float scale)
        {
            var newSize = Math.Max(BaseFontSize * scale, 4f);
            LblQueue.Font = new Font(LblQueue.Font.FontFamily, newSize, LblQueue.Font.Style);
        }

        public void SetColors(Color background, Color foreground)
        {
            BackColor = background;
            LblQueue.BackColor = background;
            LblQueue.ForeColor = foreground;
        }

        public void UpdateQueue(List<string> items) => LblQueue.Text = string.Join(Environment.NewLine, items);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }

            base.OnFormClosing(e);
        }
    }
}