using System;
using System.Windows;
using chronos_screentime.Services;

namespace chronos_screentime
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("App: Starting application...");
                
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                
                System.Diagnostics.Debug.WriteLine("App: Setting up exception handlers done");

                // Initialize WPF UI theme system at application level with system theme detection
                // This sets up global theme management that individual windows will inherit
                System.Diagnostics.Debug.WriteLine("App: Initializing WPF UI theme system...");
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                    Wpf.Ui.Appearance.ApplicationTheme.Unknown
                );
                System.Diagnostics.Debug.WriteLine("App: Theme system initialized");

                // Check for updates silently on startup (every 24 hours)
                _ = UpdateService.CheckForUpdatesSilentlyAsync();
                
                // Check for --minimized command line argument
                bool startMinimized = e.Args.Contains("--minimized");
                
                if (startMinimized)
                {
                    System.Diagnostics.Debug.WriteLine("App: Starting minimized mode...");
                    
                    // Load settings to check if tray is enabled
                    var settingsService = new Services.SettingsService();
                    var settings = settingsService.CurrentSettings;
                    
                    // Create main window but don't show it yet
                    var mainWindow = new MainWindow();
                    
                    // Hide window and minimize to tray if tray is enabled
                    if (settings.ShowInSystemTray)
                    {
                        System.Diagnostics.Debug.WriteLine("App: Minimizing to system tray...");
                        mainWindow.WindowState = WindowState.Minimized;
                        mainWindow.ShowInTaskbar = false;
                        mainWindow.Show();
                        mainWindow.Hide(); // This will trigger the tray functionality
                    }
                    else
                    {
                        // If tray is disabled, just minimize normally
                        System.Diagnostics.Debug.WriteLine("App: Minimizing normally (tray disabled)...");
                        mainWindow.WindowState = WindowState.Minimized;
                        mainWindow.Show();
                    }
                }
                else
                {
                    // Show the splash screen only when not starting minimized
                    System.Diagnostics.Debug.WriteLine("App: Creating splash screen...");
                    var splashScreen = new SplashWindow();
                    System.Diagnostics.Debug.WriteLine("App: Showing splash screen...");
                    splashScreen.Show();
                    System.Diagnostics.Debug.WriteLine("App: Splash screen shown successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App: FATAL ERROR during startup: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"App: Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Startup error: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Application Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"App: Unhandled dispatcher exception: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"App: Stack trace: {e.Exception.StackTrace}");
            MessageBox.Show($"Unhandled exception: {e.Exception.Message}\n\nStack trace: {e.Exception.StackTrace}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"App: Fatal unhandled domain exception: {ex?.Message}");
            System.Diagnostics.Debug.WriteLine($"App: Stack trace: {ex?.StackTrace}");
            MessageBox.Show($"Fatal error: {ex?.Message}\n\nStack trace: {ex?.StackTrace}", "Fatal Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
