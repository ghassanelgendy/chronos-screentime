using Microsoft.Win32;
using System;
using System.IO;

namespace chronos_screentime.Services
{
    public class StartupService
    {
        private const string StartupKeyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "ChronosScreenTime";
        
        /// <summary>
        /// Gets the current startup setting from registry
        /// </summary>
        /// <returns>"Yes", "No", or "Minimized" based on registry value</returns>
        public static string GetStartupSetting()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupKeyName))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(AppName) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            if (value.Contains("--minimized"))
                            {
                                return "Minimized";
                            }
                            else if (value.Equals(GetExecutablePath(), StringComparison.OrdinalIgnoreCase))
                            {
                                return "Yes";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking startup status: {ex.Message}");
            }
            
            return "No";
        }
        
        /// <summary>
        /// Sets the startup option with Windows
        /// </summary>
        /// <param name="option">"Yes", "No", or "Minimized"</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        public static bool SetStartupOption(string option)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupKeyName, true))
                {
                    if (key != null)
                    {
                        if (option == "No")
                        {
                            key.DeleteValue(AppName, false);
                            System.Diagnostics.Debug.WriteLine("Startup disabled");
                        }
                        else
                        {
                            var executablePath = GetExecutablePath();
                            var registryValue = option == "Minimized" 
                                ? $"{executablePath} --minimized"
                                : executablePath;
                            
                            key.SetValue(AppName, registryValue);
                            System.Diagnostics.Debug.WriteLine($"Startup set to {option}: {registryValue}");
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting startup status: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the full path to the current executable
        /// </summary>
        /// <returns>The full path to the executable</returns>
        private static string GetExecutablePath()
        {
            // Get the .exe path instead of the .dll path
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                return processPath;
            }
            
            // Fallback to assembly location if process path is not available
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exePath = Path.ChangeExtension(assemblyLocation, ".exe");
            
            if (File.Exists(exePath))
            {
                return exePath;
            }
            
            // Last fallback to assembly location
            return assemblyLocation;
        }
        
        /// <summary>
        /// Toggles the startup status through the cycle: No -> Yes -> Minimized -> No
        /// </summary>
        /// <returns>The new startup status after toggling</returns>
        public static string ToggleStartup()
        {
            var currentStatus = GetStartupSetting();
            var newStatus = currentStatus switch
            {
                "No" => "Yes",
                "Yes" => "Minimized",
                "Minimized" => "No",
                _ => "No"
            };
            
            if (SetStartupOption(newStatus))
            {
                return newStatus;
            }
            
            return currentStatus; // Return current status if toggle failed
        }
    }
} 