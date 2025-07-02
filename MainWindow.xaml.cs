using chronos_screentime.Models;
using chronos_screentime.Services;
using Hardcodet.Wpf.TaskbarNotification;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using TextBlock = System.Windows.Controls.TextBlock;

namespace chronos_screentime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        // Windows API for volume control
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutGetVolume(IntPtr hwo, out uint dwVolume);

        // Sound playback management
        private SoundPlayer? _currentSoundPlayer;
        private System.Threading.Timer? _volumeRestoreTimer;
        private readonly ScreenTimeService _screenTimeService;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly SettingsService _settingsService;
        private readonly BreakNotificationService _breakNotificationService;
        private readonly Services.IDialogService _dialogService;
        private bool _isTracking = false;
        private DateTime _trackingStartTime;
        private string _currentPeriod = "Today";
        private bool _isLoadingPageSettings = false;
        private AppSettings? _workingPageSettings;

        // System Tray functionality
        private TaskbarIcon? _taskbarIcon;
        private bool _isMinimizeToTrayEnabled = false;
        private bool _isClosingToTray = false;
        private WindowState _previousWindowState = WindowState.Normal;

        // UI Controls
        private Wpf.Ui.Controls.ToggleSwitch? PageAlwaysOnTopCheckBox;
        private Wpf.Ui.Controls.ToggleSwitch? PageShowInSystemTrayCheckBox;
        private Wpf.Ui.Controls.ToggleSwitch? PageHideTitleBarCheckBox;
        private Wpf.Ui.Controls.ToggleSwitch? PageEnableBreakNotificationsCheckBox;
        private Wpf.Ui.Controls.NumberBox? PageBreakReminderMinutesTextBox;
        private Wpf.Ui.Controls.ToggleSwitch? PageEnableScreenBreakNotificationsCheckBox;
        private Wpf.Ui.Controls.NumberBox? PageScreenBreakReminderMinutesTextBox;
        private Wpf.Ui.Controls.ToggleSwitch? PagePlaySoundWithBreakReminderCheckBox;
        private Wpf.Ui.Controls.ComboBox? PageNotificationSoundComboBox;
        private Wpf.Ui.Controls.Slider? PageNotificationVolumeSlider;
        private TextBlock? PageVolumeValueText;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize dialog service
            _dialogService = new Services.DialogService();

            // Initialize settings service first
            _settingsService = new SettingsService();

            // Apply saved theme or default to system detection
            ApplySavedTheme(_settingsService.CurrentSettings.Theme);

            // Ensure theme is properly applied after UI loads
            this.Loaded += (s, e) =>
            {
                RefreshTheme();
            };

            // Set responsive window size based on screen resolution
            SetResponsiveWindowSize();

            _screenTimeService = new ScreenTimeService();
            _screenTimeService.DataChanged += OnDataChanged;

            // Initialize system tray functionality first
            InitializeSystemTray();

            // Initialize break notification service with notification callback
            _breakNotificationService = new BreakNotificationService(_settingsService, ShowBreakNotification, () =>
            {
                // Return true if window is minimized/hidden to tray
                return !this.IsVisible || this.WindowState == WindowState.Minimized;
            });

            // Apply initial settings
            ApplySettings(_settingsService.CurrentSettings);

            // Timer to update UI every second
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiUpdateTimer.Tick += UpdateUI;
            _uiUpdateTimer.Start();

            // Refresh data when window gains focus
            this.Activated += MainWindow_Activated;

            // Start tracking by default
            StartTracking();

            // Initial UI update
            RefreshAppList();
            UpdateStatusUI();

            // Subscribe to window state change events
            this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;
        }

        #region Theme Management Methods

        /// <summary>
        /// Handles system theme changes and ensures all UI elements update properly
        /// </summary>
        private void OnSystemThemeChanged()
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Refresh the NavigationView to pick up new theme colors
                    if (MainNavigationView != null)
                    {
                        // Force a visual refresh of the NavigationView
                        MainNavigationView.UpdateLayout();
                    }

                    // Refresh any other theme-dependent elements
                    this.UpdateLayout();

                    System.Diagnostics.Debug.WriteLine("System theme change detected and UI refreshed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling theme change: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Applies the saved theme setting or defaults to system detection
        /// </summary>
        private void ApplySavedTheme(string theme)
        {
            try
            {
                var themeToApply = theme switch
                {
                    "Dark Theme" => Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    "Light Theme" => Wpf.Ui.Appearance.ApplicationTheme.Light,
                    "Auto (System)" => Wpf.Ui.Appearance.ApplicationTheme.Unknown,
                    _ => Wpf.Ui.Appearance.ApplicationTheme.Unknown // Default to system detection
                };

                System.Diagnostics.Debug.WriteLine($"Applying saved theme: {theme} -> {themeToApply}");

                // Apply theme to the application globally
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(themeToApply);

                // Apply theme to this window specifically
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

                System.Diagnostics.Debug.WriteLine($"Applied saved theme: {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying saved theme: {ex.Message}");
                // Fallback to system detection
                try
                {
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Unknown);
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback theme application also failed: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// Manually refresh theme for all UI elements (useful for debugging or forcing refresh)
        /// </summary>
        public void RefreshTheme()
        {
            try
            {
                // Reapply theme to this window with system detection
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

                // Force a complete UI refresh
                this.InvalidateVisual();
                this.UpdateLayout();

                // Refresh all child controls
                RefreshControlThemes();

                // Trigger UI refresh
                OnSystemThemeChanged();

                System.Diagnostics.Debug.WriteLine("Theme manually refreshed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing theme: {ex.Message}");
            }
        }

        private void RefreshControlThemes()
        {
            // Refresh theme for all UI elements
            RefreshTheme();
        }

        #endregion

        #region Dialog Helper Methods

        /// <summary>
        /// Shows a WPF.UI ContentDialog with the specified parameters
        /// </summary>
        private async Task<Wpf.Ui.Controls.ContentDialogResult> ShowContentDialogAsync(
            string title,
            string content,
            string primaryButtonText = "OK",
            string? secondaryButtonText = null,
            string? closeButtonText = null)
        {
            return await _dialogService.ShowContentDialogAsync(title, content, primaryButtonText, secondaryButtonText, closeButtonText);
        }

        /// <summary>
        /// Shows an information dialog
        /// </summary>
        private async Task ShowInfoDialogAsync(string title, string message)
        {
            await _dialogService.ShowInfoDialogAsync(title, message);
        }

        /// <summary>
        /// Shows a yes/no confirmation dialog
        /// </summary>
        private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            return await _dialogService.ShowConfirmationDialogAsync(title, message);
        }

        /// <summary>
        /// Shows an error dialog
        /// </summary>
        private async Task ShowErrorDialogAsync(string title, string message)
        {
            await _dialogService.ShowErrorDialogAsync(title, message);
        }

        #endregion

        #region System Tray Methods

        private void InitializeSystemTray()
        {
            try
            {
                // Try to load the icon as System.Drawing.Icon
                System.Drawing.Icon? trayIcon = null;

                try
                {
                    // Method 1: Try to load from embedded resources
                    var resourceStream = Application.GetResourceStream(new Uri("pack://application:,,,/icon.ico"));
                    if (resourceStream != null)
                    {
                        trayIcon = new System.Drawing.Icon(resourceStream.Stream);
                        System.Diagnostics.Debug.WriteLine("Successfully loaded icon from embedded resources");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to get resource stream for icon.ico");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load from embedded resources: {ex.Message}");

                    // Method 2: Try to load from file system (build output directory)
                    try
                    {
                        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                        System.Diagnostics.Debug.WriteLine($"Trying to load icon from: {iconPath}");

                        if (System.IO.File.Exists(iconPath))
                        {
                            trayIcon = new System.Drawing.Icon(iconPath);
                            System.Diagnostics.Debug.WriteLine("Successfully loaded icon from file system");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Icon file not found in base directory");
                        }
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load from file system: {ex2.Message}");

                        // Method 3: Try to extract from current application icon
                        try
                        {
                            var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                            if (appIcon != null)
                            {
                                trayIcon = appIcon;
                                System.Diagnostics.Debug.WriteLine("Successfully extracted icon from application");
                            }
                        }
                        catch (Exception ex3)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to extract application icon: {ex3.Message}");

                            // Method 4: Use a default system icon as last resort
                            try
                            {
                                trayIcon = System.Drawing.SystemIcons.Application;
                                System.Diagnostics.Debug.WriteLine("Using default system icon");
                            }
                            catch (Exception ex4)
                            {
                                System.Diagnostics.Debug.WriteLine($"Even system icon failed: {ex4.Message}");
                            }
                        }
                    }
                }

                _taskbarIcon = new TaskbarIcon
                {
                    Icon = trayIcon,
                    ToolTipText = "Chronos Screen Time Tracker",
                    Visibility = Visibility.Collapsed
                };

                // Create context menu for tray icon
                var contextMenu = new ContextMenu();

                var showMenuItem = new System.Windows.Controls.MenuItem { Header = "Show Chronos" };
                showMenuItem.Click += (s, e) => RestoreWindow();
                contextMenu.Items.Add(showMenuItem);

                contextMenu.Items.Add(new Separator());

                var exitMenuItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
                exitMenuItem.Click += (s, e) => ExitApplication();
                contextMenu.Items.Add(exitMenuItem);

                _taskbarIcon.ContextMenu = contextMenu;

                // Handle tray icon click
                _taskbarIcon.TrayLeftMouseUp += (s, e) => RestoreWindow();


            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing system tray: {ex.Message}");
                // Continue without tray functionality if initialization fails
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (!_isMinimizeToTrayEnabled) return;

            if (WindowState == WindowState.Minimized)
            {
                HideToTray();
            }
            else
            {
                _previousWindowState = WindowState;
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isMinimizeToTrayEnabled && !_isClosingToTray)
            {
                e.Cancel = true;
                HideToTray();
                ShowTrayNotification("Chronos minimized to tray",
                    "Chronos is still running in the background. Click the tray icon to restore");
            }
        }

        private void HideToTray()
        {
            try
            {
                this.Hide();
                this.ShowInTaskbar = false;

                if (_taskbarIcon != null)
                {
                    _taskbarIcon.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hiding to tray: {ex.Message}");
            }
        }

        private void RestoreWindow()
        {
            try
            {
                this.Show();
                this.ShowInTaskbar = true;
                this.WindowState = _previousWindowState;
                this.Activate();
                this.Focus();

                if (_taskbarIcon != null)
                {
                    _taskbarIcon.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring window: {ex.Message}");
            }
        }

        private void ShowTrayNotification(string title, string message)
        {
            try
            {
                _taskbarIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing tray notification: {ex.Message}");
            }
        }

        private void ShowBreakNotification(string title, string message)
        {
            try
            {
                // Show balloon tip with no icon to avoid Windows notification sound
                // Custom WAV sound will play separately (handled in BreakNotificationService)
                _taskbarIcon?.ShowBalloonTip(title, message, BalloonIcon.None);
                System.Diagnostics.Debug.WriteLine($"Break notification shown: {title} - {message} (with custom sound, no Windows sound)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in break notification: {ex.Message}");
            }
        }

        private void ExitApplication()
        {
            _isClosingToTray = false; // Allow actual closing
            Application.Current.Shutdown();
        }

        private void ApplySettings(AppSettings settings)
        {
            try
            {
                // Apply general settings
                this.Topmost = settings.AlwaysOnTop;
                this.WindowStyle = settings.HideTitleBar ? WindowStyle.None : WindowStyle.SingleBorderWindow;
                _isMinimizeToTrayEnabled = settings.ShowInSystemTray;

                // Apply system tray visibility immediately
                if (_taskbarIcon != null)
                {
                    _taskbarIcon.Visibility = settings.ShowInSystemTray ?
                        Visibility.Visible : Visibility.Collapsed;
                }

                // Apply theme setting
                ApplySavedTheme(settings.Theme);

                // Apply window state settings
                if (settings.StartMinimized && !this.IsLoaded)
                {
                    this.WindowState = WindowState.Minimized;
                    if (settings.ShowInSystemTray)
                    {
                        this.ShowInTaskbar = false;
                    }
                }

                // Sync menu items with settings
                if (AlwaysOnTopMenuItem != null)
                    AlwaysOnTopMenuItem.IsChecked = settings.AlwaysOnTop;
                if (ShowInTrayMenuItem != null)
                    ShowInTrayMenuItem.IsChecked = settings.ShowInSystemTray;
                if (HideTitleBarMenuItem != null)
                    HideTitleBarMenuItem.IsChecked = settings.HideTitleBar;

                System.Diagnostics.Debug.WriteLine($"Settings applied - AlwaysOnTop: {settings.AlwaysOnTop}, ShowInTray: {settings.ShowInSystemTray}, Theme: {settings.Theme}, HideTitleBar: {settings.HideTitleBar}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying settings: {ex.Message}");
            }
        }

        #endregion

        private void SetResponsiveWindowSize()
        {
            // Get the current screen's working area
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;

            // Calculate responsive dimensions - allow full width usage
            double targetWidth = Math.Min(screenWidth * 0.8, 1200); // Use up to 80% of screen width, max 1200px
            double targetHeight = screenHeight * 0.85; // Use up to 85% of screen height

            // Set minimum constraints only
            targetWidth = Math.Max(targetWidth, 600); // Minimum width for usability
            targetHeight = Math.Max(targetHeight, 700); // Minimum height for usability

            // Apply the calculated dimensions
            this.Width = targetWidth;
            this.Height = targetHeight;

            // Set only minimum constraints - no maximum limits
            this.MinWidth = 600;
            this.MinHeight = 700;
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // Refresh data whenever the window gains focus
            RefreshAppList();
        }

        private void TrackingStatusFooter_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isTracking)
            {
                StopTracking();
                (sender as System.Windows.Controls.TextBlock).Text = "start";
            }
            else
            {
                StartTracking();
                (sender as System.Windows.Controls.TextBlock).Text = "stop";
            }
        }

        private void StartTracking()
        {
            _isTracking = true;
            _trackingStartTime = DateTime.Now;
            _screenTimeService.StartTracking();
            UpdateUI(null, null);
        }

        private void StopTracking()
        {
            _isTracking = false;
            _screenTimeService.StopTracking();
            UpdateUI(null, null);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshAppList();
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = await ShowConfirmationDialogAsync(
                "Confirm Reset",
                "Are you sure you want to reset all tracking data? This action cannot be undone.");

            if (confirmed)
            {
                _screenTimeService.ResetAllData();
                RefreshAppList();
            }
        }

        private async void ResetAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string appName)
            {
                var confirmed = await ShowConfirmationDialogAsync(
                    "Confirm Reset",
                    $"Are you sure you want to reset tracking data for '{appName}'?");

                if (confirmed)
                {
                    _screenTimeService.ResetAppData(appName);
                    RefreshAppList();
                }
            }
        }

        private void OnDataChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => RefreshAppList());
        }

        private void RefreshAppList()
        {
            var apps = _screenTimeService.GetAllAppScreenTimes().ToList();
            AppListView.ItemsSource = apps;
            UpdateSummaryUI(apps);
            UpdateNavigationStats();
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            UpdateStatusUI();
        }

        private void UpdateStatusUI()
        {
            if (_isTracking)
            {
                var sessionDuration = DateTime.Now - _trackingStartTime;
                SessionTimeText.Text = $"{sessionDuration:hh\\:mm\\:ss}";
                StatusIndicator.Fill = (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            }
            else
            {
                SessionTimeText.Text = "Not tracking";
                StatusIndicator.Fill = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            }

            // Update tray icon tooltip with current screen time
            UpdateTrayTooltip();
        }

        private void UpdateTrayTooltip()
        {
            if (_taskbarIcon != null)
            {
                // Get total time including current active session
                var totalTime = _screenTimeService.GetTotalScreenTimeTodayIncludingCurrent();
                var hours = (int)totalTime.TotalHours;
                var minutes = totalTime.Minutes;

                string message = $"Chronos - You spent {hours}h {minutes}m on your screen today";
                if (hours > 10)
                {
                    message += " - Do you need help?";
                }
                else if (hours > 6)
                {
                    message += " - chill bud";
                }

                _taskbarIcon.ToolTipText = message;
            }
        }

        private void UpdateNavigationStats()
        {
            var apps = _screenTimeService.GetAllAppScreenTimes().ToList();

            // Update sidebar stats if NavigationView is being used
            if (SidebarCurrentPeriod != null)
                SidebarCurrentPeriod.Text = _currentPeriod;

            if (SidebarTotalTime != null)
            {
                var totalTime = apps.Sum(a => a.TotalTime.TotalMinutes);
                var hours = (int)(totalTime / 60);
                var minutes = (int)(totalTime % 60);
                SidebarTotalTime.Text = $"{hours}h {minutes}m";
            }

            if (SidebarSwitches != null)
                SidebarSwitches.Text = $"{_screenTimeService.TotalSwitches} switches";

            if (SidebarApps != null)
                SidebarApps.Text = $"{apps.Count} apps";
        }

        private void UpdateSummaryUI(System.Collections.Generic.List<AppScreenTime> apps)
        {
            TotalAppsText.Text = apps.Count.ToString();

            var totalTime = apps.Sum(a => a.TotalTime.TotalMinutes);
            var hours = (int)(totalTime / 60);
            var minutes = (int)(totalTime % 60);
            TotalTimeText.Text = $"{hours}h {minutes}m";

            TotalSwitchesText.Text = _screenTimeService.TotalSwitches.ToString();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _ = ShowErrorDialogAsync("Error", $"Unable to open link: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Stop any playing sounds and dispose resources
            StopCurrentSound();

            // Clean up event handlers
            this.Activated -= MainWindow_Activated;
            this.StateChanged -= MainWindow_StateChanged;
            this.Closing -= MainWindow_Closing;

            _screenTimeService?.Dispose();
            _uiUpdateTimer?.Stop();
            _breakNotificationService?.Dispose();

            // Dispose of tray icon resources
            if (_taskbarIcon != null)
            {
                _taskbarIcon.Dispose();
                _taskbarIcon = null;
            }

            base.OnClosed(e);
        }

        #region Menu Event Handlers

        // File Menu
        private async void OpenDataFile_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Open Data File feature coming soon!");
        }

        private async void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Export to CSV feature coming soon!");
        }

        private async void ExportCharts_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Export Charts feature coming soon!");
        }

        private async void AutoExportSettings_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Auto Export Settings feature coming soon!");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _isClosingToTray = false; // Ensure we actually exit, not minimize to tray
            this.Close();
        }

        // View Menu
        private async void ShowPieChart_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Pie Chart by Category feature coming soon!");
        }

        private async void ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Bar Chart by Apps feature coming soon!");
        }

        private async void ShowLiveDashboard_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Live Dashboard feature coming soon!");
        }

        private async void ManageCategories_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Manage App Categories feature coming soon!");
        }

        private async void ViewByCategory_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "View by Category feature coming soon!");
        }

        private async void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem != null)
            {
                this.Topmost = menuItem.IsChecked;
                await ShowInfoDialogAsync("Setting Changed",
                    $"Always on top: {(menuItem.IsChecked ? "Enabled" : "Disabled")}");
            }
        }

        private async void ShowInTray_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem != null && _taskbarIcon != null)
            {
                var newValue = menuItem.IsChecked;

                System.Diagnostics.Debug.WriteLine($"ShowInTray_Click: Toggling ShowInSystemTray to {newValue}");

                // Update the setting through SettingsService
                _settingsService.UpdateSettings(s => s.ShowInSystemTray = newValue);

                // Apply the change immediately
                _isMinimizeToTrayEnabled = newValue;

                // Update tray icon visibility immediately
                _taskbarIcon.Visibility = newValue ? Visibility.Visible : Visibility.Collapsed;

                if (_isMinimizeToTrayEnabled)
                {
                    ShowTrayNotification("Tray mode enabled",
                        "Chronos will now minimize to the system tray instead of the taskbar. " +
                        "Closing the window will also minimize to tray.");
                }
                else
                {
                    // If window is currently hidden and tray mode is being disabled, restore it
                    if (!this.IsVisible)
                    {
                        RestoreWindow();
                    }

                    await ShowInfoDialogAsync("Tray Mode", "Tray mode disabled. The application will now behave normally.");
                }
            }
            else if (_taskbarIcon == null)
            {
                await ShowInfoDialogAsync("Tray Unavailable", "System tray functionality is not available. This may be due to icon loading issues or system limitations.");
            }
        }

        private async void HideTitleBar_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem != null)
            {
                this.WindowStyle = menuItem.IsChecked ? WindowStyle.None : WindowStyle.SingleBorderWindow;
                await ShowInfoDialogAsync("Setting Changed",
                    $"Title bar: {(menuItem.IsChecked ? "Hidden" : "Visible")}");
            }
        }

        // Tools Menu - Tracking
        private async void TrackIdleTime_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Track Idle Time feature coming soon!");
        }

        private async void TrackSubProcesses_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Track Sub-processes feature coming soon!");
        }

        private async void ProcessTreeAnalysis_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Process Tree Analysis feature coming soon!");
        }

        private async void MostUsedApps_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Most Used Apps feature coming soon!");
        }

        // Tools Menu - Productivity
        private async void BreakNotifications_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.CurrentSettings;
            var isEnabled = settings.EnableBreakNotifications;

            var confirmed = await ShowConfirmationDialogAsync(
                "Break Notifications",
                $"Break notifications are currently {(isEnabled ? "enabled" : "disabled")}.\n\n" +
                $"Current reminder interval: {settings.BreakReminderMinutes} minutes\n\n" +
                $"Would you like to {(isEnabled ? "disable" : "enable")} break notifications?");

            if (confirmed)
            {
                _settingsService.UpdateSettings(s => s.EnableBreakNotifications = !isEnabled);

                await ShowInfoDialogAsync("Settings  ",
                    $"Break notifications have been {(!isEnabled ? "enabled" : "disabled")}.\n\n" +
                    "You can adjust detailed settings in the Preferences window.");
            }
        }

        private async void ScreenBreakNotifications_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.CurrentSettings;
            var isEnabled = settings.EnableScreenBreakNotifications;

            var confirmed = await ShowConfirmationDialogAsync(
                "Screen Break Notifications",
                $"Screen break notifications (20-20-20 rule) are currently {(isEnabled ? "enabled" : "disabled")}.\n\n" +
                $"Current reminder interval: {settings.ScreenBreakReminderMinutes} minutes\n" +
                $"Break duration: {settings.ScreenBreakDurationSeconds} seconds\n\n" +
                $"This feature reminds you to look at something 20 feet away for 20 seconds every 20 minutes to protect your eye health.\n\n" +
                $"Would you like to {(isEnabled ? "disable" : "enable")} screen break notifications?");

            if (confirmed)
            {
                _settingsService.UpdateSettings(s => s.EnableScreenBreakNotifications = !isEnabled);

                await ShowInfoDialogAsync("Settings  ",
                    $"Screen break notifications have been {(!isEnabled ? "enabled" : "disabled")}.\n\n" +
                    "You can adjust detailed settings in the Preferences window.");
            }
        }

        private async void AutoLogoutSettings_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Auto Logout Settings feature coming soon!");
        }

        private async void DistractionBlocking_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Distraction Blocking feature coming soon!");
        }

        private async void SetGoals_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Daily/Weekly Goals feature coming soon!");
        }

        // Tools Menu - Data Management
        private async void MergeEntries_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Merge Consecutive Entries feature coming soon!");
        }

        private async void BackupSync_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Backup & Sync feature coming soon!");
        }

        private async void CleanOldData_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Clean Old Data feature coming soon!");
        }

        private void ShowPreferences_Click(object sender, RoutedEventArgs e)
        {
            ShowPreferencesPage();
        }

        #region Page Navigation Methods

        private void ShowPreferencesPage()
        {
            try
            {
                // Load settings into UI
                LoadSettings();

                // Show preferences overlay with animation
                if (PreferencesOverlay != null)
                {
                    PreferencesOverlay.Visibility = Visibility.Visible;
                    PreferencesOverlay.RenderTransform = new TranslateTransform();

                    var storyboard = new Storyboard();

                    // Fade in animation
                    var fadeInAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromSeconds(0.3),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(fadeInAnimation, PreferencesOverlay);
                    Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));
                    storyboard.Children.Add(fadeInAnimation);

                    // Slide up animation
                    var slideUpAnimation = new DoubleAnimation
                    {
                        From = 30,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.3),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(slideUpAnimation, PreferencesOverlay);
                    Storyboard.SetTargetProperty(slideUpAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                    storyboard.Children.Add(slideUpAnimation);

                    storyboard.Begin();
                }

                // Show background overlay
                if (PreferencesOverlayBackground != null)
                {
                    PreferencesOverlayBackground.Visibility = Visibility.Visible;
                    PreferencesOverlayBackground.Opacity = 0;

                    var fadeInAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 0.5,
                        Duration = TimeSpan.FromSeconds(0.3),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    PreferencesOverlayBackground.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
                }

                System.Diagnostics.Debug.WriteLine("Preferences page shown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing preferences page: {ex.Message}");
            }
        }

        private void BackToMain_Click(object sender, RoutedEventArgs e)
        {
            HidePreferencesPage();
        }

        private void HidePreferencesPage()
        {
            try
            {
                // Hide preferences overlay with animation
                if (PreferencesOverlay != null)
                {
                    var storyboard = new Storyboard();

                    // Fade out animation
                    var fadeOutAnimation = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.3),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };
                    Storyboard.SetTarget(fadeOutAnimation, PreferencesOverlay);
                    Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(UIElement.OpacityProperty));
                    storyboard.Children.Add(fadeOutAnimation);

                    // Slide down animation
                    var slideDownAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 30,
                        Duration = TimeSpan.FromSeconds(0.3),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };
                    Storyboard.SetTarget(slideDownAnimation, PreferencesOverlay);
                    Storyboard.SetTargetProperty(slideDownAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                    storyboard.Children.Add(slideDownAnimation);

                    storyboard.Completed += (s, e) =>
                    {
                        PreferencesOverlay.Visibility = Visibility.Collapsed;
                    };
                    storyboard.Begin();
                }

                // Hide background overlay
                if (PreferencesOverlayBackground != null)
                {
                    var fadeOutAnimation = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.3),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };
                    fadeOutAnimation.Completed += (s, e) =>
                    {
                        PreferencesOverlayBackground.Visibility = Visibility.Collapsed;
                    };
                    PreferencesOverlayBackground.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                }

                // Save settings if they were changed
                var changedSettings = SavePageUIToWorkingSettings();
                if (changedSettings.Count > 0)
                {
                    // Apply the working settings
                    _settingsService.CurrentSettings = _workingPageSettings.Clone();
                    _settingsService.SaveSettings();

                    // Show confirmation with the list of changes
                    var message = string.Join("\n", changedSettings);
                    ShowSaveConfirmation(message);
                }

                System.Diagnostics.Debug.WriteLine("Preferences page hidden");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hiding preferences page: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                _isLoadingPageSettings = true;

                var settings = _settingsService.CurrentSettings;

                // General Settings
                SetPageCheckBoxValue("PageAlwaysOnTopCheckBox", settings.AlwaysOnTop);
                SetPageCheckBoxValue("PageShowInSystemTrayCheckBox", settings.ShowInSystemTray);
                SetPageCheckBoxValue("PageHideTitleBarCheckBox", settings.HideTitleBar);

                // Break Notifications
                SetPageCheckBoxValue("PageEnableBreakNotificationsCheckBox", settings.EnableBreakNotifications);
                SetPageTextBoxValue("PageBreakReminderMinutesTextBox", settings.BreakReminderMinutes.ToString());

                // Screen Break Notifications
                SetPageCheckBoxValue("PageEnableScreenBreakNotificationsCheckBox", settings.EnableScreenBreakNotifications);
                SetPageTextBoxValue("PageScreenBreakReminderMinutesTextBox", settings.ScreenBreakReminderMinutes.ToString());
                SetPageCheckBoxValue("PagePlaySoundWithBreakReminderCheckBox", settings.PlaySoundWithBreakReminder);

                // Notification Sound
                PopulatePageNotificationSoundComboBox();
                if (PageNotificationSoundComboBox != null)
                {
                    var soundItems = PageNotificationSoundComboBox.Items.Cast<Wpf.Ui.Controls.ComboBoxItem>();
                    var selectedItem = soundItems.FirstOrDefault(item => item.Content?.ToString() == settings.NotificationSoundFile);
                    if (selectedItem != null)
                    {
                        PageNotificationSoundComboBox.SelectedItem = selectedItem;
                    }
                }

                // Notification Volume
                if (PageNotificationVolumeSlider != null)
                {
                    PageNotificationVolumeSlider.Value = settings.NotificationVolume;
                }

                // Create working copy of settings
                _workingPageSettings = settings.Clone();

                System.Diagnostics.Debug.WriteLine("Settings loaded to overlay UI");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings to overlay UI: {ex.Message}");
            }
            finally
            {
                _isLoadingPageSettings = false;
            }
        }

        private void PopulatePageNotificationSoundComboBox()
        {
            try
            {
                if (PageNotificationSoundComboBox != null)
                {
                    PageNotificationSoundComboBox.Items.Clear();

                    // Get all .wav files from the assets/wav directory
                    var wavFiles = Directory.GetFiles("assets/wav", "*.wav")
                                         .Select(Path.GetFileNameWithoutExtension)
                                         .OrderBy(name => name);

                    foreach (var wavFile in wavFiles)
                    {
                        var item = new Wpf.Ui.Controls.ComboBoxItem
                        {
                            Content = wavFile
                        };
                        PageNotificationSoundComboBox.Items.Add(item);
                    }

                    // Select the first item by default if none is selected
                    if (PageNotificationSoundComboBox.SelectedItem == null && PageNotificationSoundComboBox.Items.Count > 0)
                    {
                        PageNotificationSoundComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating notification sound combo box: {ex.Message}");
            }
        }

        private void PageNotificationSoundComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoadingPageSettings && PageNotificationSoundComboBox != null && PageNotificationSoundComboBox.SelectedItem is Wpf.Ui.Controls.ComboBoxItem selectedItem)
            {
                var selectedSound = selectedItem.Content?.ToString();
                if (!string.IsNullOrEmpty(selectedSound))
                {
                    PlaySoundPreview(selectedSound);
                }
            }
        }

        private void PageNotificationVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoadingPageSettings && sender is Wpf.Ui.Controls.Slider slider && PageVolumeValueText != null)
            {
                var volume = (int)slider.Value;
                PageVolumeValueText.Text = $"{volume}%";

                // If a sound is currently selected, play it with the new volume
                if (PageNotificationSoundComboBox?.SelectedItem is Wpf.Ui.Controls.ComboBoxItem selectedItem)
                {
                    var selectedSound = selectedItem.Content?.ToString();
                    if (!string.IsNullOrEmpty(selectedSound))
                    {
                        PlaySoundPreviewWithVolume(selectedSound, volume);
                    }
                }
            }
        }

        private void PlaySoundPreview(string soundFileName)
        {
            if (PageNotificationVolumeSlider != null)
            {
                PlaySoundPreviewWithVolume(soundFileName, (int)PageNotificationVolumeSlider.Value);
            }
        }

        private void PlaySoundPreviewWithVolume(string soundFileName, int volume)
        {
            try
            {
                var soundPath = Path.Combine("assets", "wav", $"{soundFileName}.wav");
                if (File.Exists(soundPath))
                {
                    PlaySoundWithVolumeControl(soundPath, volume);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound preview: {ex.Message}");
            }
        }

        private void PlaySoundWithVolumeControl(string soundPath, int volumePercent)
        {
            try
            {
                // Stop any currently playing sound
                StopCurrentSound();

                // Convert percentage (0-100) to Windows volume format (0x0000 to 0xFFFF for each channel)
                uint volume = (uint)((volumePercent / 100.0) * 0xFFFF);
                uint stereoVolume = (volume << 16) | volume; // Set both left and right channels

                // Get current system volume
                waveOutGetVolume(IntPtr.Zero, out uint originalVolume);

                // Set temporary volume
                waveOutSetVolume(IntPtr.Zero, stereoVolume);

                // Create and play sound asynchronously
                _currentSoundPlayer = new SoundPlayer(soundPath);
                _currentSoundPlayer.Load(); // Load the sound file
                _currentSoundPlayer.Play(); // Play asynchronously

                // Set up timer to restore volume after sound duration (estimate 2 seconds max for preview)
                _volumeRestoreTimer?.Dispose();
                _volumeRestoreTimer = new System.Threading.Timer(
                    callback: _ =>
                    {
                        try
                        {
                            // Restore original volume
                            waveOutSetVolume(IntPtr.Zero, originalVolume);
                            System.Diagnostics.Debug.WriteLine("Volume restored to original level");
                        }
                        catch (Exception restoreEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error restoring volume: {restoreEx.Message}");
                        }
                    },
                    state: null,
                    dueTime: 2000, // Restore volume after 2 seconds
                    period: System.Threading.Timeout.Infinite);

                System.Diagnostics.Debug.WriteLine($"Playing sound with {volumePercent}% volume (async)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound with volume: {ex.Message}");
                // Fallback to regular sound player
                try
                {
                    StopCurrentSound();
                    _currentSoundPlayer = new SoundPlayer(soundPath);
                    _currentSoundPlayer.Play();
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback sound playback also failed: {fallbackEx.Message}");
                }
            }
        }

        private void StopCurrentSound()
        {
            try
            {
                // Stop current sound if playing
                if (_currentSoundPlayer != null)
                {
                    _currentSoundPlayer.Stop();
                    _currentSoundPlayer.Dispose();
                    _currentSoundPlayer = null;
                }

                // Cancel volume restore timer if active
                _volumeRestoreTimer?.Dispose();
                _volumeRestoreTimer = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping current sound: {ex.Message}");
            }
        }

        private void SaveSettingsFromOverlay()
        {
            try
            {
                // Get current settings before changes
                var oldSettings = _settingsService.CurrentSettings;
                var changedSettings = new List<string>();

                // Save UI values to working settings and get list of changes
                changedSettings = SavePageUIToWorkingSettings();

                _settingsService.UpdateSettings(settings =>
                {
                    // General Settings
                    if (PageAlwaysOnTopCheckBox != null && settings.AlwaysOnTop != (PageAlwaysOnTopCheckBox.IsChecked == true))
                    {
                        settings.AlwaysOnTop = PageAlwaysOnTopCheckBox.IsChecked == true;
                        changedSettings.Add($"Always on top is now {(settings.AlwaysOnTop ? "enabled" : "disabled")}");
                    }

                    if (PageShowInSystemTrayCheckBox != null && settings.ShowInSystemTray != (PageShowInSystemTrayCheckBox.IsChecked == true))
                    {
                        settings.ShowInSystemTray = PageShowInSystemTrayCheckBox.IsChecked == true;
                        changedSettings.Add($"Close to tray is now {(settings.ShowInSystemTray ? "enabled" : "disabled")}");
                    }

                    if (PageHideTitleBarCheckBox != null && settings.HideTitleBar != (PageHideTitleBarCheckBox.IsChecked == true))
                    {
                        settings.HideTitleBar = PageHideTitleBarCheckBox.IsChecked == true;
                        changedSettings.Add($"Hide title bar is now {(settings.HideTitleBar ? "enabled" : "disabled")}");
                    }

                    // Break Notifications
                    if (PageEnableBreakNotificationsCheckBox != null && settings.EnableBreakNotifications != (PageEnableBreakNotificationsCheckBox.IsChecked == true))
                    {
                        settings.EnableBreakNotifications = PageEnableBreakNotificationsCheckBox.IsChecked == true;
                        changedSettings.Add($"Break notifications {(settings.EnableBreakNotifications ? "enabled" : "disabled")}");
                    }

                    if (PageBreakReminderMinutesTextBox != null && int.TryParse(PageBreakReminderMinutesTextBox.Text, out int breakMinutes) && breakMinutes > 0 && settings.BreakReminderMinutes != breakMinutes)
                    {
                        settings.BreakReminderMinutes = breakMinutes;
                        changedSettings.Add($"Break reminder interval changed to {breakMinutes} minutes");
                    }

                    // Screen Break Notifications
                    if (PageEnableScreenBreakNotificationsCheckBox != null && settings.EnableScreenBreakNotifications != (PageEnableScreenBreakNotificationsCheckBox.IsChecked == true))
                    {
                        settings.EnableScreenBreakNotifications = PageEnableScreenBreakNotificationsCheckBox.IsChecked == true;
                        changedSettings.Add($"Screen break notifications {(settings.EnableScreenBreakNotifications ? "enabled" : "disabled")}");
                    }

                    if (PageScreenBreakReminderMinutesTextBox != null && int.TryParse(PageScreenBreakReminderMinutesTextBox.Text, out int screenBreakMinutes) && screenBreakMinutes > 0 && settings.ScreenBreakReminderMinutes != screenBreakMinutes)
                    {
                        settings.ScreenBreakReminderMinutes = screenBreakMinutes;
                        changedSettings.Add($"Screen break interval changed to {screenBreakMinutes} minutes");
                    }

                    if (PagePlaySoundWithBreakReminderCheckBox != null && settings.PlaySoundWithBreakReminder != (PagePlaySoundWithBreakReminderCheckBox.IsChecked == true))
                    {
                        settings.PlaySoundWithBreakReminder = PagePlaySoundWithBreakReminderCheckBox.IsChecked == true;
                        changedSettings.Add($"Sound with break reminders is now {(settings.PlaySoundWithBreakReminder ? "enabled" : "disabled")}");
                    }
                });

                // Apply settings immediately
                ApplySettings(_settingsService.CurrentSettings);

                // Show specific save confirmation
                if (changedSettings.Count > 0)
                {
                    var message = changedSettings.Count == 1
                        ? $"✓ {changedSettings[0]}"
                        : $"✓ {changedSettings.Count} settings updated";
                    ShowSaveConfirmation(message);

                    System.Diagnostics.Debug.WriteLine($"Settings saved from page: {changedSettings.Count} changes");
                }
                else
                {
                    ShowSaveConfirmation("✓ No changes to save");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings from page: {ex.Message}");
                ShowSaveConfirmation("✗ Error saving changes");
            }
        }

        private void ShowSaveConfirmation(string message = "✓ Changes saved")
        {
            try
            {
                var snackbar = new Wpf.Ui.Controls.Snackbar
                {
                    Title = "Settings Saved",
                    Content = message,
                    Appearance = Wpf.Ui.Appearance.ControlAppearance.Success,
                    Icon = Wpf.Ui.Common.SymbolRegular.CheckmarkCircle24,
                    Timeout = TimeSpan.FromSeconds(3)
                };

                snackbar.Show();

                System.Diagnostics.Debug.WriteLine($"Save confirmation shown: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing save confirmation: {ex.Message}");
            }
        }

        private async void ResetPreferencesToDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = await ShowConfirmationDialogAsync(
                "Reset to Defaults",
                "Are you sure you want to reset all preferences to default values?");

            if (confirmed)
            {

                _settingsService.ResetToDefaults();
                LoadSettings();
                ApplySettings(_settingsService.CurrentSettings);

                // ThemedMessageBox.Show(this, "Preferences have been reset to defaults.", "Reset Complete", 
                //               ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);

                try
                {
                    // Reset working settings to defaults
                    _workingPageSettings = new AppSettings();

                    // Save to service
                    _settingsService.SaveSettings(_workingPageSettings);

                    // Reload overlay with default settings
                    LoadSettings();

                    // Apply settings immediately
                    ApplySettings(_settingsService.CurrentSettings);

                    // Show confirmation
                    ShowSaveConfirmation("✓ Reset to defaults");

                    System.Diagnostics.Debug.WriteLine("Preferences reset to defaults successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error resetting preferences: {ex.Message}");
                    ShowSaveConfirmation("✗ Error resetting preferences");
                }
            }
        }

        private void ApplyPreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsFromPage();
                // Save confirmation is now shown by SaveSettingsFromPage method
            }
            catch (Exception ex)
            {

                // ThemedMessageBox.Show(this, $"Error applying preferences: {ex.Message}", "Error", 
                //               ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Error);
            }
        }

        private void OKPreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsFromOverlay();
                HidePreferencesOverlay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying preferences: {ex.Message}");
                ShowErrorDialogAsync("Error", $"Error applying preferences: {ex.Message}");
            }
        }

        #endregion

        // Help Menu
        private async void ShowTutorial_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Tutorial Mode feature coming soon!");
        }

        private async void ShowShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var shortcuts = @"Keyboard Shortcuts:

File Menu:
• Ctrl+O - Open Data File
• Ctrl+E - Export to CSV
• Ctrl+Q - Quit Application

View Menu:
• F1 - Show Pie Chart
• F2 - Show Bar Chart
• F3 - Show Live Dashboard
• Ctrl+T - Toggle Always on Top

Tools Menu:
• Ctrl+, - Show Preferences
• Ctrl+G - Set Goals
• Ctrl+B - Break Notifications
• Ctrl+S - Screen Break Notifications

General:
• F5 - Refresh Data
• Ctrl+R - Reset All Data
• Space - Start/Stop Tracking";

            await ShowInfoDialogAsync("Keyboard Shortcuts", shortcuts);
        }

        private async void OpenSource_Click(object sender, RoutedEventArgs e)
        {
            var url = "https://github.com/ghassanelgendy/chronos-screentime";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            await ShowInfoDialogAsync("Open Source",
                $"Opening GitHub repository:\n{url}\n\nChronos Screen Time Tracker is open source!");
        }

        private async void ShowAbout_Click(object sender, RoutedEventArgs e)
        {
            var about = @"Chronos Screen Time Tracker v1.0

A comprehensive application usage tracking tool for Windows.

Features:
• Real-time application monitoring
• Cumulative time tracking
• Application switching analytics
• Data persistence and export
• Productivity insights

© 2025 - Built with .NET 8.0 and WPF
Open Source Software";

            await ShowInfoDialogAsync("About Chronos", about);
        }

        #endregion

        #region Date Navigation Methods

        private void ShowToday_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Today";
            TimeLabel.Text = "Today's Screen Time";
            SwitchesLabel.Text = "Today's Switches";

            // Show main screen time content
            ShowMainContent();
            RefreshAppList();
        }

        private void ShowMainContent()
        {
            try
            {
                // Hide title bar back button
                if (TitleBarBackButton != null)
                    TitleBarBackButton.Visibility = Visibility.Collapsed;

                // If preferences are currently shown, animate the transition
                if (PreferencesContent.Visibility == Visibility.Visible)
                {
                    // Animate out preferences content
                    var fadeOutStoryboard = (Storyboard)this.Resources["FadeOutDownAnimation"];
                    if (fadeOutStoryboard != null)
                    {
                        Storyboard.SetTarget(fadeOutStoryboard, PreferencesContent);
                        fadeOutStoryboard.Completed += (s, e) =>
                        {
                            PreferencesContent.Visibility = Visibility.Collapsed;

                            // Show and animate in main content
                            ScreenTimeContent.Visibility = Visibility.Visible;
                            var fadeInStoryboard = (Storyboard)this.Resources["FadeInUpAnimation"];
                            if (fadeInStoryboard != null)
                            {
                                Storyboard.SetTarget(fadeInStoryboard, ScreenTimeContent);
                                fadeInStoryboard.Begin();
                            }
                        };
                        fadeOutStoryboard.Begin();
                    }
                    else
                    {
                        // Fallback without animation
                        PreferencesContent.Visibility = Visibility.Collapsed;
                        ScreenTimeContent.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    // Just ensure main content is visible
                    ScreenTimeContent.Visibility = Visibility.Visible;
                }

                System.Diagnostics.Debug.WriteLine("Main content shown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing main content: {ex.Message}");
            }
        }

        private async void ShowYesterday_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Yesterday";
            TimeLabel.Text = "Yesterday's Screen Time";
            SwitchesLabel.Text = "Yesterday's Switches";

            // Show main content first
            ShowMainContent();

            // TODO: Load yesterday's data
            await ShowInfoDialogAsync("Feature Preview", "Yesterday's data feature coming soon!");
        }

        private async void ShowThisWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "This Week";
            TimeLabel.Text = "This Week's Screen Time";
            SwitchesLabel.Text = "This Week's Switches";

            // Show main content first
            ShowMainContent();

            await ShowInfoDialogAsync("Feature Preview", "Weekly view feature coming soon!");
        }

        private async void ShowLastWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Last Week";
            TimeLabel.Text = "Last Week's Screen Time";
            SwitchesLabel.Text = "Last Week's Switches";

            // Show main content first
            ShowMainContent();

            await ShowInfoDialogAsync("Feature Preview", "Last week view feature coming soon!");
        }

        private async void ShowThisMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "This Month";
            TimeLabel.Text = "This Month's Screen Time";
            SwitchesLabel.Text = "This Month's Switches";

            // Show main content first
            ShowMainContent();

            await ShowInfoDialogAsync("Feature Preview", "Monthly view feature coming soon!");
        }

        private async void ShowCustomRange_Click(object sender, RoutedEventArgs e)
        {
            // Show main content first
            ShowMainContent();

            await ShowInfoDialogAsync("Feature Preview", "Custom date range picker coming soon!");
        }

        #endregion

        #region Category Filter Methods

        private async void FilterByCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string category)
            {
                await ShowInfoDialogAsync("Feature Preview", $"Filtering by {category} category coming soon!");
            }
        }

        private void ShowAllCategories_Click(object sender, RoutedEventArgs e)
        {
            RefreshAppList();
        }

        private async void ShowWeeklyReport_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Weekly report feature coming soon!");
        }

        #endregion

        #region Settings Helper Methods

        private void SetPageCheckBoxValue(string name, bool value)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.ToggleSwitch toggleSwitch)
            {
                toggleSwitch.IsChecked = value;
            }
        }

        private void SetPageTextBoxValue(string name, string value)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.NumberBox numberBox)
            {
                numberBox.Value = double.Parse(value);
            }
        }

        private bool GetPageCheckBoxValue(string name)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.ToggleSwitch toggleSwitch)
            {
                return toggleSwitch.IsChecked == true;
            }
            return false;
        }

        private int GetPageIntTextBoxValue(string name, int defaultValue)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.NumberBox numberBox)
            {
                if (numberBox.Value > 0)
                {
                    return (int)numberBox.Value;
                }
            }
            return defaultValue;
        }

        private List<string> SavePageUIToWorkingSettings()
        {
            var changedSettings = new List<string>();

            try
            {
                if (_workingPageSettings == null)
                    _workingPageSettings = new AppSettings();

                var oldSettings = _settingsService.CurrentSettings;

                // General Settings
                var newAlwaysOnTop = GetPageCheckBoxValue("PageAlwaysOnTopCheckBox");
                if (_workingPageSettings.AlwaysOnTop != newAlwaysOnTop)
                {
                    _workingPageSettings.AlwaysOnTop = newAlwaysOnTop;
                    changedSettings.Add($"Always on top is now {(newAlwaysOnTop ? "enabled" : "disabled")}");
                }

                var newShowInSystemTray = GetPageCheckBoxValue("PageShowInSystemTrayCheckBox");
                if (_workingPageSettings.ShowInSystemTray != newShowInSystemTray)
                {
                    _workingPageSettings.ShowInSystemTray = newShowInSystemTray;
                    changedSettings.Add($"Close to tray is now {(newShowInSystemTray ? "enabled" : "disabled")}");
                }

                var newHideTitleBar = GetPageCheckBoxValue("PageHideTitleBarCheckBox");
                if (_workingPageSettings.HideTitleBar != newHideTitleBar)
                {
                    _workingPageSettings.HideTitleBar = newHideTitleBar;
                    changedSettings.Add($"Hide title bar is now {(newHideTitleBar ? "enabled" : "disabled")}");
                }

                // Break Notifications
                var newEnableBreakNotifications = GetPageCheckBoxValue("PageEnableBreakNotificationsCheckBox");
                if (_workingPageSettings.EnableBreakNotifications != newEnableBreakNotifications)
                {
                    _workingPageSettings.EnableBreakNotifications = newEnableBreakNotifications;
                    changedSettings.Add($"Break notifications are now {(newEnableBreakNotifications ? "enabled" : "disabled")}");
                }

                var newBreakReminderMinutes = GetPageIntTextBoxValue("PageBreakReminderMinutesTextBox", 30);
                if (_workingPageSettings.BreakReminderMinutes != newBreakReminderMinutes)
                {
                    _workingPageSettings.BreakReminderMinutes = newBreakReminderMinutes;
                    changedSettings.Add($"Break reminder interval changed to {newBreakReminderMinutes} minutes");
                }

                // Screen Break Notifications
                var newEnableScreenBreakNotifications = GetPageCheckBoxValue("PageEnableScreenBreakNotificationsCheckBox");
                if (_workingPageSettings.EnableScreenBreakNotifications != newEnableScreenBreakNotifications)
                {
                    _workingPageSettings.EnableScreenBreakNotifications = newEnableScreenBreakNotifications;
                    changedSettings.Add($"Screen break notifications are now {(newEnableScreenBreakNotifications ? "enabled" : "disabled")}");
                }

                var newScreenBreakReminderMinutes = GetPageIntTextBoxValue("PageScreenBreakReminderMinutesTextBox", 20);
                if (_workingPageSettings.ScreenBreakReminderMinutes != newScreenBreakReminderMinutes)
                {
                    _workingPageSettings.ScreenBreakReminderMinutes = newScreenBreakReminderMinutes;
                    changedSettings.Add($"Screen break interval changed to {newScreenBreakReminderMinutes} minutes");
                }

                var newPlaySoundWithBreakReminder = GetPageCheckBoxValue("PagePlaySoundWithBreakReminderCheckBox");
                if (_workingPageSettings.PlaySoundWithBreakReminder != newPlaySoundWithBreakReminder)
                {
                    _workingPageSettings.PlaySoundWithBreakReminder = newPlaySoundWithBreakReminder;
                    changedSettings.Add($"Sound with break reminders is now {(newPlaySoundWithBreakReminder ? "enabled" : "disabled")}");
                }

                // Notification Sound
                if (PageNotificationSoundComboBox != null && PageNotificationSoundComboBox.SelectedItem is string selectedSound)
                {
                    if (_workingPageSettings.NotificationSoundFile != selectedSound)
                    {
                        _workingPageSettings.NotificationSoundFile = selectedSound;
                        changedSettings.Add($"Notification sound changed to {selectedSound}");
                    }
                }

                // Notification Volume
                if (PageNotificationVolumeSlider != null)
                {
                    var newVolume = (int)PageNotificationVolumeSlider.Value;
                    if (_workingPageSettings.NotificationVolume != newVolume)
                    {
                        _workingPageSettings.NotificationVolume = newVolume;
                        changedSettings.Add($"Notification volume changed to {newVolume}%");
                    }
                }

                System.Diagnostics.Debug.WriteLine("Overlay UI saved to working settings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving overlay UI to working settings: {ex.Message}");
            }

            return changedSettings;
        }

        #endregion

        private void SaveSettingsFromPage()
        {
            try
            {
                SaveSettingsFromOverlay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                ShowSaveConfirmation("✗ Error saving settings");
            }
        }

        private void HidePreferencesOverlay()
        {
            HidePreferencesPage();
        }

        private void ShowSaveConfirmation(string message = "✓ Changes saved")
        {
            try
            {
                var snackbar = new Wpf.Ui.Controls.Snackbar
                {
                    Title = "Settings Saved",
                    Content = message,
                    Appearance = Wpf.Ui.Appearance.ControlAppearance.Success,
                    Icon = Wpf.Ui.Common.SymbolRegular.CheckmarkCircle24,
                    Timeout = TimeSpan.FromSeconds(3)
                };

                snackbar.Show();

                System.Diagnostics.Debug.WriteLine($"Save confirmation shown: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing save confirmation: {ex.Message}");
            }
        }
    }
}
