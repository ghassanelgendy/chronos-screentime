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

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private delegate bool EnumChildWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildWindowsProc lpEnumFunc, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

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

                // Get process information for the foreground window
                GetWindowThreadProcessId(hwnd, out uint processId);
                using Process process = Process.GetProcessById((int)processId);

                // UWP apps run inside ApplicationFrameHost – get the real app process from the first child with a different PID
                uint effectiveProcessId = processId;
                if (process.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                {
                    uint? childPid = GetFirstChildProcessId(hwnd, processId);
                    if (childPid.HasValue)
                        effectiveProcessId = childPid.Value;
                }

                if (effectiveProcessId != processId)
                {
                    try
                    {
                        using Process realProcess = Process.GetProcessById((int)effectiveProcessId);
                        return new ActiveWindowInfo
                        {
                            WindowTitle = windowTitle.ToString(),
                            ProcessName = realProcess.ProcessName,
                            ProcessPath = GetProcessPath(realProcess),
                            ProcessId = effectiveProcessId
                        };
                    }
                    catch
                    {
                        // Child process may have exited; fall back to frame host
                    }
                }

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
                System.Diagnostics.Debug.WriteLine($"Error getting active window: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// For an ApplicationFrameHost window, returns the process ID of the first child window that belongs to a different process (the real UWP app).
        /// </summary>
        private static uint? GetFirstChildProcessId(IntPtr hwndParent, uint parentProcessId)
        {
            uint? found = null;
            EnumChildWindowsProc callback = (IntPtr hwndChild, IntPtr lParam) =>
            {
                GetWindowThreadProcessId(hwndChild, out uint childPid);
                if (childPid != 0 && childPid != parentProcessId)
                {
                    found = childPid;
                    return false; // stop enumeration
                }
                return true;
            };
            EnumChildWindows(hwndParent, callback, IntPtr.Zero);
            return found;
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

        public uint GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);
            return (uint)Environment.TickCount - lastInputInfo.dwTime;
        }
    }
}