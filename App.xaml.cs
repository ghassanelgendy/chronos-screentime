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
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Check for updates silently on startup (every 24 hours)
            await UpdateService.CheckForUpdatesSilentlyAsync();
        }

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

                // Show the splash screen
                System.Diagnostics.Debug.WriteLine("App: Creating splash screen...");
                var splashScreen = new SplashWindow();
                System.Diagnostics.Debug.WriteLine("App: Showing splash screen...");
                splashScreen.Show();
                System.Diagnostics.Debug.WriteLine("App: Splash screen shown successfully");
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
