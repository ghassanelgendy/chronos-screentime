using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace chronos_screentime.Windows
{
    public partial class SplashWindow : Window
    {
        private DispatcherTimer _timer;
        private DateTime _startTime;

        public SplashWindow()
        {
            InitializeComponent();

            // Load and apply saved theme setting
            try
            {
                var settingsService = new chronos_screentime.Services.SettingsService();
                var theme = settingsService.CurrentSettings.Theme;

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
                this.InvalidateVisual();
                this.UpdateLayout();
            }
            catch
            {
                // Fallback to system detection if settings can't be loaded
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            }

            _startTime = DateTime.Now;

            // Start the loading animation
            StartLoadingAnimation();

            // Set up timer to close splash screen after 2 seconds
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void StartLoadingAnimation()
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

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer.Stop();

            // Close the splash screen first with fade out animation
            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            fadeOut.Completed += (s, args) =>
            {
                // Create and show the main window after splash screen fades out
                var mainWindow = new MainWindow();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();

                // Close this splash window
                this.Close();
            };

            this.BeginAnimation(OpacityProperty, fadeOut);
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}