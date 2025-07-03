using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace chronos_screentime
{
    public partial class SplashWindow : Window
    {
        private DispatcherTimer _timer;
        private DateTime _startTime;

        public SplashWindow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Splash: Starting initialization...");
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("Splash: InitializeComponent completed");

                // Load and apply saved theme setting
                try
                {
                    System.Diagnostics.Debug.WriteLine("Splash: Loading settings service...");
                    var settingsService = new chronos_screentime.Services.SettingsService();
                    var theme = settingsService.CurrentSettings.Theme;
                    System.Diagnostics.Debug.WriteLine($"Splash: Loaded theme setting: {theme}");

                    var themeToApply = theme switch
                    {
                        "Dark Theme" => Wpf.Ui.Appearance.ApplicationTheme.Dark,
                        "Light Theme" => Wpf.Ui.Appearance.ApplicationTheme.Light,
                        "Auto (System)" => Wpf.Ui.Appearance.ApplicationTheme.Unknown,
                        _ => Wpf.Ui.Appearance.ApplicationTheme.Unknown
                    };

                    System.Diagnostics.Debug.WriteLine($"Splash: Applying theme {theme} -> {themeToApply}");

                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(themeToApply);
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

                    // Force refresh of this window
                    System.Diagnostics.Debug.WriteLine("Splash: Refreshing window visuals...");
                    this.InvalidateVisual();
                    this.UpdateLayout();
                    System.Diagnostics.Debug.WriteLine("Splash: Window visuals refreshed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Splash: Error applying theme: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Splash: Stack trace: {ex.StackTrace}");
                    // Fallback to system detection if settings can't be loaded
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
                }

                _startTime = DateTime.Now;

                // Start the loading animation
                System.Diagnostics.Debug.WriteLine("Splash: Starting loading animation...");
                StartLoadingAnimation();
                System.Diagnostics.Debug.WriteLine("Splash: Loading animation started");

                // Set up timer to close splash screen after 2 seconds
                System.Diagnostics.Debug.WriteLine("Splash: Setting up close timer...");
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
                System.Diagnostics.Debug.WriteLine("Splash: Close timer started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash: FATAL ERROR during initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Splash: Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error initializing splash screen: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw; // Re-throw to be caught by the application handler
            }
        }

        private void StartLoadingAnimation()
        {
            try
            {
                // Animate the loading progress bar
                var progressAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 300, // Full width of the progress bar
                    Duration = TimeSpan.FromSeconds(1.8), // Slightly faster than the timer
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                LoadingProgress.BeginAnimation(WidthProperty, progressAnimation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash: Error in loading animation: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Splash: Stack trace: {ex.StackTrace}");
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Splash: Timer tick - starting transition to main window...");
                _timer.Stop();

                // Close the splash screen first with fade out animation
                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    FillBehavior = FillBehavior.Stop // Stop the animation at the end
                };

                fadeOut.Completed += (s, args) =>
                {
                    try
                    {
                        // Ensure window is completely invisible
                        this.Opacity = 0;
                        this.Visibility = Visibility.Hidden;

                        System.Diagnostics.Debug.WriteLine("Splash: Creating main window...");
                        var mainWindow = new MainWindow();
                        Application.Current.MainWindow = mainWindow;
                        System.Diagnostics.Debug.WriteLine("Splash: Showing main window...");
                        mainWindow.Show();
                        System.Diagnostics.Debug.WriteLine("Splash: Main window shown successfully");

                        // Close this splash window
                        System.Diagnostics.Debug.WriteLine("Splash: Closing splash window...");
                        this.Close();
                        System.Diagnostics.Debug.WriteLine("Splash: Splash window closed");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Splash: Error transitioning to main window: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Splash: Stack trace: {ex.StackTrace}");
                        MessageBox.Show($"Error loading main window: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                this.BeginAnimation(OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash: Error in timer tick: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Splash: Stack trace: {ex.StackTrace}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Splash: OnClosed called");
                _timer?.Stop();
                base.OnClosed(e);
                System.Diagnostics.Debug.WriteLine("Splash: OnClosed completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash: Error in OnClosed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Splash: Stack trace: {ex.StackTrace}");
            }
        }
    }
}