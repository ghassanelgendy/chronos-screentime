using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace chronos_screentime.Services
{
    public class Win32ApiService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public class ActiveWindowInfo
        {
            public string WindowTitle { get; set; } = string.Empty;
            public string ProcessName { get; set; } = string.Empty;
            public string ProcessPath { get; set; } = string.Empty;
            public uint ProcessId { get; set; }
        }

        public ActiveWindowInfo? GetActiveWindow()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return null;

                // Get window title
                int length = GetWindowTextLength(hwnd);
                StringBuilder windowTitle = new StringBuilder(length + 1);
                GetWindowText(hwnd, windowTitle, windowTitle.Capacity);

                // Get process information
                GetWindowThreadProcessId(hwnd, out uint processId);
                using Process process = Process.GetProcessById((int)processId);

                return new ActiveWindowInfo
                {
                    WindowTitle = windowTitle.ToString(),
                    ProcessName = process.ProcessName,
                    ProcessPath = GetProcessPath(process),
                    ProcessId = processId
                };
            }
            catch (Exception ex)
            {
                // Log error or handle gracefully
                System.Diagnostics.Debug.WriteLine($"Error getting active window: {ex.Message}");
                return null;
            }
        }

        private string GetProcessPath(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
} 