// DebugConsole.cs - Lightweight in-app log viewer window
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FireKeeper
{
    public static class DebugConsole
    {
        private static Form _form;
        private static TextBox _textBox;
        private static readonly object _lock = new object();
        private static bool _isVisible = false;

        public static void Show()
        {
            lock (_lock)
            {
                if (_form != null && !_form.IsDisposed)
                {
                    _form.Show();
                    _form.BringToFront();
                    _isVisible = true;
                    return;
                }

                _form = new Form
                {
                    Text = "FireKeeper Debug Console",
                    Size = new System.Drawing.Size(900, 600),
                    StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(50, 50),
                    FormBorderStyle = FormBorderStyle.Sizable
                };

                _textBox = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10),
                    BackColor = Color.Black,
                    ForeColor = Color.LimeGreen,
                    ReadOnly = true
                };

                _form.Controls.Add(_textBox);
                _form.FormClosing += (s, e) =>
                {
                    _isVisible = false;
                    e.Cancel = true;
                    _form.Hide();
                };
                _form.Show();
                _isVisible = true;
                Log("Debug console opened.");
            }
        }

        public static void Hide()
        {
            lock (_lock)
            {
                if (_form != null && !_form.IsDisposed)
                {
                    _form.Hide();
                    _isVisible = false;
                }
            }
        }

        public static void Toggle()
        {
            if (_isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                
                if (_form != null && !_form.IsDisposed && _textBox != null && !_textBox.IsDisposed)
                {
                    if (_textBox.InvokeRequired)
                    {
                        _textBox.Invoke(new Action(() =>
                        {
                            _textBox.AppendText(line + Environment.NewLine);
                            _textBox.SelectionStart = _textBox.Text.Length;
                            _textBox.ScrollToCaret();
                        }));
                    }
                    else
                    {
                        _textBox.AppendText(line + Environment.NewLine);
                        _textBox.SelectionStart = _textBox.Text.Length;
                        _textBox.ScrollToCaret();
                    }
                }
                
                try
                {
                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "FireKeeper", "debug.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.AppendAllText(logPath, line + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
