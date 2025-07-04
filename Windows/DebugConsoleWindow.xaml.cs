using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text;
using System.ComponentModel;

namespace chronos_screentime.Windows
{
    public class DebugTextWriter : TextWriter
    {
        private StringBuilder _buffer = new StringBuilder();
        private Action<string> _onTextChanged;

        public DebugTextWriter(Action<string> onTextChanged)
        {
            _onTextChanged = onTextChanged;
        }

        public override void Write(char value)
        {
            _buffer.Append(value);
            _onTextChanged?.Invoke(_buffer.ToString());
        }

        public override void Write(string? value)
        {
            if (value != null)
            {
                _buffer.Append(value);
                _onTextChanged?.Invoke(_buffer.ToString());
            }
        }

        public override Encoding Encoding => Encoding.UTF8;

        public void Clear()
        {
            _buffer.Clear();
            _onTextChanged?.Invoke(string.Empty);
        }

        public string GetText() => _buffer.ToString();
    }

    public partial class DebugConsoleWindow : Wpf.Ui.Controls.FluentWindow
    {
        private static DebugConsoleWindow? _instance;
        private readonly DebugTextWriter _debugWriter;
        private readonly TextWriterTraceListener _traceListener;
        private bool _forceClose;

        public static DebugConsoleWindow Instance
        {
            get
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new DebugConsoleWindow();
                }
                return _instance;
            }
        }

        private DebugConsoleWindow()
        {
            InitializeComponent();

            // Set up debug writer and trace listener
            _debugWriter = new DebugTextWriter(text =>
            {
                Dispatcher.Invoke(() =>
                {
                    LogTextBox.Text = text;
                    LogTextBox.ScrollToEnd();
                });
            });
            _traceListener = new TextWriterTraceListener(_debugWriter);
            Trace.Listeners.Add(_traceListener);

            // Handle window closing
            this.Closing += DebugConsoleWindow_Closing;

            // Handle application shutdown
            Application.Current.Exit += (s, e) =>
            {
                _forceClose = true;
                CleanupAndClose(true);
            };

            // Handle main window closing
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Closing += (s, e) =>
                {
                    _forceClose = true;
                    CleanupAndClose(true);
                };
            }
        }

        private void DebugConsoleWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_forceClose)
            {
                e.Cancel = true;  // Don't actually close, just hide
                this.Hide();
            }
        }

        private void CleanupAndClose(bool force = false)
        {
            try
            {
                if (force)
                {
                    _forceClose = true;
                }

                // Remove trace listener
                if (_traceListener != null && Trace.Listeners.Contains(_traceListener))
                {
                    Trace.Listeners.Remove(_traceListener);
                }

                // Dispose writer
                _debugWriter?.Dispose();

                // Clear static instance
                _instance = null;

                if (force && this.IsLoaded)
                {
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during debug console cleanup: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _debugWriter.Clear();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".log",
                FileName = $"chronos-debug-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, _debugWriter.GetText());
                    MessageBox.Show("Log file saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving log file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            CleanupAndClose();
        }
    }
} 