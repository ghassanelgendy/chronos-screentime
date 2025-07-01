using System.Configuration;
using System.Data;
using System.Windows;
using System;
using chronos_screentime.Windows;

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
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                
                // Initialize WPF UI theme system at application level with system theme detection
                // This sets up global theme management that individual windows will inherit
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                    Wpf.Ui.Appearance.ApplicationTheme.Unknown
                );
                
                // Show the splash screen
                var splashScreen = new SplashWindow();
                splashScreen.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Application Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled exception: {e.Exception.Message}\n\nStack trace: {e.Exception.StackTrace}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Fatal error: {ex?.Message}\n\nStack trace: {ex?.StackTrace}", "Fatal Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

}
