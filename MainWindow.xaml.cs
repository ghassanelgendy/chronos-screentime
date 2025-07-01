using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using chronos_screentime.Models;
using chronos_screentime.Services;
using chronos_screentime.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Runtime.InteropServices;

namespace chronos_screentime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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
        private bool _isTracking = false;
        private DateTime _trackingStartTime;
        private bool _isSidebarOpen = false;
        private string _currentPeriod = "Today";
        private bool _isLoadingOverlaySettings = false;
        private AppSettings? _workingOverlaySettings;
        
        // System Tray functionality
        private TaskbarIcon? _taskbarIcon;
        private bool _isMinimizeToTrayEnabled = false;
        private bool _isClosingToTray = false;
        private WindowState _previousWindowState = WindowState.Normal;

        public MainWindow()
        {
            InitializeComponent();
            
            // Set responsive window size based on screen resolution
            SetResponsiveWindowSize();
            
            _screenTimeService = new ScreenTimeService();
            _screenTimeService.DataChanged += OnDataChanged;
            
            // Initialize settings service
            _settingsService = new SettingsService();
            
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
                
                var showMenuItem = new MenuItem { Header = "Show Chronos" };
                showMenuItem.Click += (s, e) => RestoreWindow();
                contextMenu.Items.Add(showMenuItem);
                
                contextMenu.Items.Add(new Separator());
                
                var exitMenuItem = new MenuItem { Header = "Exit" };
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
                
                // Sync menu items with settings
                if (AlwaysOnTopMenuItem != null)
                    AlwaysOnTopMenuItem.IsChecked = settings.AlwaysOnTop;
                if (ShowInTrayMenuItem != null)
                    ShowInTrayMenuItem.IsChecked = settings.ShowInSystemTray;
                if (HideTitleBarMenuItem != null)
                    HideTitleBarMenuItem.IsChecked = settings.HideTitleBar;
                
                System.Diagnostics.Debug.WriteLine("Settings applied to main window and menu items synced");
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
            
            // Calculate responsive dimensions using 550x931 ratio for 1920x1080
            // Width ratio: 550/1920 ≈ 0.287
            // Height ratio: 931/1080 ≈ 0.862
            double targetWidth = screenWidth * 0.308;
            double targetHeight = screenHeight * 0.862;
            
            // Set minimum and maximum constraints
            targetWidth = Math.Max(targetWidth, 450); // Minimum width for usability
            targetHeight = Math.Max(targetHeight, 600); // Minimum height for usability
            
            targetWidth = Math.Min(targetWidth, 650); // Maximum width to maintain tall/thin ratio
            targetHeight = Math.Min(targetHeight, 1100); // Maximum height for very large screens
            
            // Apply the calculated dimensions
            this.Width = targetWidth;
            this.Height = targetHeight;
            
            // Set min/max constraints
            this.MinWidth = 450;
            this.MinHeight = 600;
            this.MaxWidth = 650;
            this.MaxHeight = 1100;
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
            }
            else
            {
                StartTracking();
            }
        }

        private void StartTracking()
        {
            _isTracking = true;
            _trackingStartTime = DateTime.Now;
            _screenTimeService.StartTracking();
            
            StatusIndicator.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)); // Green
            
            TrackingStatusPrefix.Text = "Screentime is currently being tracked click to ";
            TrackingActionWord.Text = "stop";
        }

        private void StopTracking()
        {
            _isTracking = false;
            _screenTimeService.StopTracking();
            
            StatusIndicator.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // Red
            SessionTimeText.Text = "Not tracking";
            
            TrackingStatusPrefix.Text = "Screentime is currently not being tracked click to ";
            TrackingActionWord.Text = "start tracking";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshAppList();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = ThemedMessageBox.Show(
                this,
                "Are you sure you want to reset all tracking data? This action cannot be undone.",
                "Confirm Reset",
                ThemedMessageBox.MessageButtons.YesNo,
                ThemedMessageBox.MessageType.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                _screenTimeService.ResetAllData();
                RefreshAppList();
            }
        }

        private void ResetAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string appName)
            {
                var result = ThemedMessageBox.Show(
                    this,
                    $"Are you sure you want to reset tracking data for '{appName}'?",
                    "Confirm Reset",
                    ThemedMessageBox.MessageButtons.YesNo,
                    ThemedMessageBox.MessageType.Question);
                    
                if (result == MessageBoxResult.Yes)
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
            
            if (_isSidebarOpen)
            {
                UpdateSidebarStats();
            }
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
                ThemedMessageBox.Show(
                    this,
                    $"Unable to open link: {ex.Message}",
                    "Error",
                    ThemedMessageBox.MessageButtons.OK,
                    ThemedMessageBox.MessageType.Error);
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
        private void OpenDataFile_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Open Data File feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Export to CSV feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ExportCharts_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Export Charts feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void AutoExportSettings_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Auto Export Settings feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _isClosingToTray = false; // Ensure we actually exit, not minimize to tray
            this.Close();
        }

        // View Menu
        private void ShowPieChart_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Pie Chart by Category feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Bar Chart by Apps feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ShowLiveDashboard_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Live Dashboard feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ManageCategories_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Manage App Categories feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ViewByCategory_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "View by Category feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                this.Topmost = menuItem.IsChecked;
                ThemedMessageBox.Show(this, $"Always on top: {(menuItem.IsChecked ? "Enabled" : "Disabled")}", 
                              "Setting Changed", ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            }
        }

        private void ShowInTray_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null && _taskbarIcon != null)
            {
                var newValue = menuItem.IsChecked;
                
                // Update the setting through SettingsService
                _settingsService.UpdateSettings(s => s.ShowInSystemTray = newValue);
                
                // Apply the change immediately
                _isMinimizeToTrayEnabled = newValue;
                
                if (_isMinimizeToTrayEnabled)
                {
                    ShowTrayNotification("Tray mode enabled", 
                        "Chronos will now minimize to the system tray instead of the taskbar. " +
                        "Closing the window will also minimize to tray.");
                }
                else
                {
                    // Hide tray icon if currently visible
                    if (_taskbarIcon.Visibility == Visibility.Visible)
                    {
                        _taskbarIcon.Visibility = Visibility.Collapsed;
                        if (!this.IsVisible)
                        {
                            RestoreWindow();
                        }
                    }
                    
                    ThemedMessageBox.Show(this, "Tray mode disabled. The application will now behave normally.", 
                              "Tray Mode", ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
                }
            }
            else if (_taskbarIcon == null)
            {
                ThemedMessageBox.Show(this, "System tray functionality is not available. This may be due to icon loading issues or system limitations.", 
                              "Tray Unavailable", ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Warning);
            }
        }

        private void HideTitleBar_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                this.WindowStyle = menuItem.IsChecked ? WindowStyle.None : WindowStyle.SingleBorderWindow;
                ThemedMessageBox.Show(this, $"Title bar: {(menuItem.IsChecked ? "Hidden" : "Visible")}", 
                              "Setting Changed", ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            }
        }

        // Tools Menu - Tracking
        private void TrackIdleTime_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Track Idle Time feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void TrackSubProcesses_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Track Sub-processes feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ProcessTreeAnalysis_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Process Tree Analysis feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void MostUsedApps_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Most Used Apps feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        // Tools Menu - Productivity
        private void BreakNotifications_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.CurrentSettings;
            var isEnabled = settings.EnableBreakNotifications;
            
            var result = ThemedMessageBox.Show(this, 
                $"Break notifications are currently {(isEnabled ? "enabled" : "disabled")}.\n\n" +
                $"Current reminder interval: {settings.BreakReminderMinutes} minutes\n\n" +
                $"Would you like to {(isEnabled ? "disable" : "enable")} break notifications?", 
                "Break Notifications", 
                ThemedMessageBox.MessageButtons.YesNo, 
                ThemedMessageBox.MessageType.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                _settingsService.UpdateSettings(s => s.EnableBreakNotifications = !isEnabled);
                
                ThemedMessageBox.Show(this, 
                    $"Break notifications have been {(!isEnabled ? "enabled" : "disabled")}.\n\n" +
                    "You can adjust detailed settings in the Preferences window.", 
                    "Settings Updated", 
                    ThemedMessageBox.MessageButtons.OK, 
                    ThemedMessageBox.MessageType.Information);
            }
        }

        private void ScreenBreakNotifications_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.CurrentSettings;
            var isEnabled = settings.EnableScreenBreakNotifications;
            
            var result = ThemedMessageBox.Show(this, 
                $"Screen break notifications (20-20-20 rule) are currently {(isEnabled ? "enabled" : "disabled")}.\n\n" +
                $"Current reminder interval: {settings.ScreenBreakReminderMinutes} minutes\n" +
                $"Break duration: {settings.ScreenBreakDurationSeconds} seconds\n\n" +
                $"This feature reminds you to look at something 20 feet away for 20 seconds every 20 minutes to protect your eye health.\n\n" +
                $"Would you like to {(isEnabled ? "disable" : "enable")} screen break notifications?", 
                "Screen Break Notifications", 
                ThemedMessageBox.MessageButtons.YesNo, 
                ThemedMessageBox.MessageType.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                _settingsService.UpdateSettings(s => s.EnableScreenBreakNotifications = !isEnabled);
                
                ThemedMessageBox.Show(this, 
                    $"Screen break notifications have been {(!isEnabled ? "enabled" : "disabled")}.\n\n" +
                    "You can adjust detailed settings in the Preferences window.", 
                    "Settings Updated", 
                    ThemedMessageBox.MessageButtons.OK, 
                    ThemedMessageBox.MessageType.Information);
            }
        }

        private void AutoLogoutSettings_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Auto Logout Settings feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void DistractionBlocking_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Distraction Blocking feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void SetGoals_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Daily/Weekly Goals feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        // Tools Menu - Data Management
        private void MergeEntries_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Merge Consecutive Entries feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void BackupSync_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Backup &amp; Sync feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void CleanOldData_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Clean Old Data feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ShowPreferences_Click(object sender, RoutedEventArgs e)
        {
            ShowPreferencesOverlay();
        }

        #region Preferences Overlay Methods

        private void ShowPreferencesOverlay()
        {
            try
            {
                // Load current settings into overlay controls
                LoadSettingsToOverlay();
                
                // Show overlay
                PreferencesOverlayBackground.Visibility = Visibility.Visible;
                PreferencesOverlay.Visibility = Visibility.Visible;
                
                // Animate in
                var showStoryboard = (Storyboard)this.Resources["ShowPreferencesOverlayStoryboard"];
                showStoryboard.Begin();
                
                System.Diagnostics.Debug.WriteLine("Preferences overlay shown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing preferences overlay: {ex.Message}");
            }
        }

        private void ClosePreferencesOverlay_Click(object sender, RoutedEventArgs e)
        {
            HidePreferencesOverlay();
        }

        private void HidePreferencesOverlay()
        {
            try
            {
                // Stop any playing sound previews
                StopCurrentSound();
                
                // Animate out
                var hideStoryboard = (Storyboard)this.Resources["HidePreferencesOverlayStoryboard"];
                hideStoryboard.Completed += (s, e) => {
                    PreferencesOverlayBackground.Visibility = Visibility.Collapsed;
                    PreferencesOverlay.Visibility = Visibility.Collapsed;
                };
                hideStoryboard.Begin();
                
                System.Diagnostics.Debug.WriteLine("Preferences overlay hidden");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hiding preferences overlay: {ex.Message}");
            }
        }

        private void LoadSettingsToOverlay()
        {
            try
            {
                _isLoadingOverlaySettings = true;
                
                // Create working copy of settings
                _workingOverlaySettings = _settingsService.CurrentSettings.Clone();
                
                // Populate notification sound options
                PopulateOverlayNotificationSoundComboBox();
                
                // General Settings
                SetOverlayCheckBoxValue("OverlayAlwaysOnTopCheckBox", _workingOverlaySettings.AlwaysOnTop);
                SetOverlayCheckBoxValue("OverlayShowInSystemTrayCheckBox", _workingOverlaySettings.ShowInSystemTray);
                SetOverlayCheckBoxValue("OverlayHideTitleBarCheckBox", _workingOverlaySettings.HideTitleBar);
                
                // Break Notifications
                SetOverlayCheckBoxValue("OverlayEnableBreakNotificationsCheckBox", _workingOverlaySettings.EnableBreakNotifications);
                SetOverlayTextBoxValue("OverlayBreakReminderMinutesTextBox", _workingOverlaySettings.BreakReminderMinutes.ToString());
                
                // Screen Break Notifications
                SetOverlayCheckBoxValue("OverlayEnableScreenBreakNotificationsCheckBox", _workingOverlaySettings.EnableScreenBreakNotifications);
                SetOverlayTextBoxValue("OverlayScreenBreakReminderMinutesTextBox", _workingOverlaySettings.ScreenBreakReminderMinutes.ToString());
                SetOverlayCheckBoxValue("OverlayPlaySoundWithBreakReminderCheckBox", _workingOverlaySettings.PlaySoundWithBreakReminder);
                
                // Notification sound selection
                if (OverlayNotificationSoundComboBox != null)
                    OverlayNotificationSoundComboBox.SelectedItem = _workingOverlaySettings.NotificationSoundFile;
                
                // Notification volume
                if (OverlayNotificationVolumeSlider != null && OverlayVolumeValueText != null)
                {
                    OverlayNotificationVolumeSlider.Value = _workingOverlaySettings.NotificationVolume;
                    OverlayVolumeValueText.Text = $"{_workingOverlaySettings.NotificationVolume}%";
                }
                
                System.Diagnostics.Debug.WriteLine("Settings loaded to overlay");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings to overlay: {ex.Message}");
            }
            finally
            {
                _isLoadingOverlaySettings = false;
            }
        }

        private void PopulateOverlayNotificationSoundComboBox()
        {
            try
            {
                if (OverlayNotificationSoundComboBox != null)
                {
                    string wavDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "wav");
                    if (System.IO.Directory.Exists(wavDir))
                    {
                        var files = System.IO.Directory.GetFiles(wavDir, "*.wav").Select(System.IO.Path.GetFileName).ToList();
                        OverlayNotificationSoundComboBox.Items.Clear();
                        foreach (var file in files)
                        {
                            OverlayNotificationSoundComboBox.Items.Add(file);
                        }
                        
                        // Add event handler for selection change to play preview
                        OverlayNotificationSoundComboBox.SelectionChanged -= OverlayNotificationSoundComboBox_SelectionChanged;
                        OverlayNotificationSoundComboBox.SelectionChanged += OverlayNotificationSoundComboBox_SelectionChanged;
                        
                        // Select current setting or first item
                        var currentSetting = _settingsService.CurrentSettings.NotificationSoundFile;
                        if (!string.IsNullOrEmpty(currentSetting) && files.Contains(currentSetting))
                        {
                            OverlayNotificationSoundComboBox.SelectedItem = currentSetting;
                        }
                        else if (files.Count > 0)
                        {
                            OverlayNotificationSoundComboBox.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating overlay notification sound ComboBox: {ex.Message}");
            }
        }

        private void OverlayNotificationSoundComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Only play preview if user is actively selecting (not during initial load)
                if (!_isLoadingOverlaySettings && sender is ComboBox comboBox && comboBox.SelectedItem is string selectedSoundFile)
                {
                    PlaySoundPreview(selectedSoundFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound preview: {ex.Message}");
            }
        }

        private void OverlayNotificationVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // Update the volume percentage display
                if (OverlayVolumeValueText != null)
                {
                    OverlayVolumeValueText.Text = $"{(int)e.NewValue}%";
                }
                
                // Only play preview sound if user is actively changing volume (not during initial load)
                if (!_isLoadingOverlaySettings && OverlayNotificationSoundComboBox?.SelectedItem is string selectedSoundFile)
                {
                    PlaySoundPreviewWithVolume(selectedSoundFile, (int)e.NewValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling volume slider change: {ex.Message}");
            }
        }

        private void PlaySoundPreview(string soundFileName)
        {
            // Get current volume from slider or default to 50
            int volume = OverlayNotificationVolumeSlider?.Value != null ? (int)OverlayNotificationVolumeSlider.Value : 50;
            PlaySoundPreviewWithVolume(soundFileName, volume);
        }

        private void PlaySoundPreviewWithVolume(string soundFileName, int volume)
        {
            try
            {
                string wavDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "wav");
                string soundPath = System.IO.Path.Combine(wavDir, soundFileName);
                
                if (File.Exists(soundPath))
                {
                    // Use volume-controlled sound playback
                    PlaySoundWithVolumeControl(soundPath, volume);
                    System.Diagnostics.Debug.WriteLine($"Playing sound preview: {soundFileName} at {volume}% volume");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Sound file not found: {soundPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound file {soundFileName}: {ex.Message}");
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
                    period: System.Threading.Timeout.Infinite
                );

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
                // Save UI values to working settings and get list of changes
                var changedSettings = SaveOverlayUIToWorkingSettings();
                
                // Save working settings to service
                if (_workingOverlaySettings != null)
                {
                    _settingsService.SaveSettings(_workingOverlaySettings);
                    
                    // Apply settings immediately
                    ApplySettings(_settingsService.CurrentSettings);
                    
                    // Show detailed confirmation message (no modal, just fading text)
                    if (changedSettings.Count > 0)
                    {
                        var message = changedSettings.Count == 1 
                            ? $"✓ {changedSettings[0]}" 
                            : $"✓ {changedSettings.Count} changes saved";
                        ShowSaveConfirmation(message);
                    }
                    else
                    {
                        ShowSaveConfirmation("✓ No changes to save");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Settings saved from overlay: {changedSettings.Count} changes");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings from overlay: {ex.Message}");
                ShowSaveConfirmation("✗ Error saving changes");
                throw;
            }
        }

        private void ShowSaveConfirmation(string message = "✓ Changes saved")
        {
            try
            {
                if (SaveConfirmationMessage != null)
                {
                    // Set the specific message
                    SaveConfirmationMessage.Text = message;
                    
                    // Reset opacity and show message
                    SaveConfirmationMessage.Opacity = 1;
                    
                    // Start fade out animation
                    var fadeStoryboard = (Storyboard)this.Resources["SaveConfirmationFadeOutStoryboard"];
                    fadeStoryboard?.Begin();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing save confirmation: {ex.Message}");
            }
        }

        private void ResetPreferencesToDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = ThemedMessageBox.Show(
                this,
                "Are you sure you want to reset all preferences to default values?",
                "Reset to Defaults",
                ThemedMessageBox.MessageButtons.YesNo,
                ThemedMessageBox.MessageType.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Reset working settings to defaults
                    _workingOverlaySettings = new AppSettings();
                    
                    // Save to service
                    _settingsService.SaveSettings(_workingOverlaySettings);
                    
                    // Reload overlay with default settings
                    LoadSettingsToOverlay();
                    
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
                SaveSettingsFromOverlay();
                // Save confirmation is now shown by SaveSettingsFromOverlay method
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(this, $"Error applying preferences: {ex.Message}", "Error", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Error);
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
                ThemedMessageBox.Show(this, $"Error saving preferences: {ex.Message}", "Error", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Error);
            }
        }

        private void CancelPreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reload original settings without saving
                LoadSettingsToOverlay();
                
                // Hide overlay
                HidePreferencesOverlay();
                
                // Show confirmation
                ShowSaveConfirmation("Changes discarded");
                
                System.Diagnostics.Debug.WriteLine("Preferences changes cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cancelling preferences: {ex.Message}");
                HidePreferencesOverlay(); // Hide overlay anyway
            }
        }

        #endregion

        // Help Menu
        private void ShowTutorial_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Tutorial Mode feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ShowShortcuts_Click(object sender, RoutedEventArgs e)
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

            ThemedMessageBox.Show(this, shortcuts, "Keyboard Shortcuts", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void OpenSource_Click(object sender, RoutedEventArgs e)
        {
            var url = "https://github.com/ghassanelgendy/chronos-screentime";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            ThemedMessageBox.Show(this, $"Opening GitHub repository:\n{url}\n\nChronos Screen Time Tracker is open source!",
                          "Open Source", ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void ShowAbout_Click(object sender, RoutedEventArgs e)
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

            ThemedMessageBox.Show(this, about, "About Chronos", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        #endregion

        #region Sidebar Methods

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (_isSidebarOpen)
            {
                CloseSidebar();
            }
            else
            {
                OpenSidebar();
            }
        }

        private void OpenSidebar()
        {
            _isSidebarOpen = true;
            Sidebar.Visibility = Visibility.Visible;
            SidebarBackground.Visibility = Visibility.Visible;
            
            var showStoryboard = (Storyboard)FindResource("ShowSidebarStoryboard");
            showStoryboard.Begin();
            
            UpdateSidebarStats();
        }

        private void CloseSidebar()
        {
            _isSidebarOpen = false;
            
            var hideStoryboard = (Storyboard)FindResource("HideSidebarStoryboard");
            hideStoryboard.Completed += (s, e) =>
            {
                Sidebar.Visibility = Visibility.Collapsed;
                SidebarBackground.Visibility = Visibility.Collapsed;
            };
            hideStoryboard.Begin();
        }

        private void CloseSidebar_Click(object sender, RoutedEventArgs e)
        {
            CloseSidebar();
        }

        private void CloseSidebarBackground_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CloseSidebar();
        }

        private void UpdateSidebarStats()
        {
            SidebarCurrentPeriod.Text = _currentPeriod;
            
            var apps = _screenTimeService.GetAllAppScreenTimes().ToList();
            var totalTime = apps.Sum(a => a.TodaysTime.TotalMinutes);
            var hours = (int)(totalTime / 60);
            var minutes = (int)(totalTime % 60);
            
            SidebarTotalTime.Text = $"{hours}h {minutes}m";
            SidebarSwitches.Text = $"{_screenTimeService.TotalSwitches} switches";
            SidebarApps.Text = $"{apps.Count(a => a.TodaysTime.TotalSeconds > 0)} apps";
        }

        #endregion

        #region Date Navigation Methods

        private void ShowToday_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Today";
            TimeLabel.Text = "Today's Screen Time";
            SwitchesLabel.Text = "Today's Switches";
            RefreshAppList();
            UpdateSidebarStats();
            CloseSidebar();
        }

        private void ShowYesterday_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Yesterday";
            TimeLabel.Text = "Yesterday's Screen Time";
            SwitchesLabel.Text = "Yesterday's Switches";
            
            // TODO: Load yesterday's data
            ThemedMessageBox.Show(this, "Yesterday's data feature coming soon!", "Feature Preview",
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            CloseSidebar();
        }

        private void ShowThisWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "This Week";
            TimeLabel.Text = "This Week's Screen Time";
            SwitchesLabel.Text = "This Week's Switches";
            
            ThemedMessageBox.Show(this, "Weekly view feature coming soon!", "Feature Preview",
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            CloseSidebar();
        }

        private void ShowLastWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Last Week";
            TimeLabel.Text = "Last Week's Screen Time";
            SwitchesLabel.Text = "Last Week's Switches";
            
            ThemedMessageBox.Show(this, "Last week view feature coming soon!", "Feature Preview",
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            CloseSidebar();
        }

        private void ShowThisMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "This Month";
            TimeLabel.Text = "This Month's Screen Time";
            SwitchesLabel.Text = "This Month's Switches";
            
            ThemedMessageBox.Show(this, "Monthly view feature coming soon!", "Feature Preview",
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            CloseSidebar();
        }

        private void ShowCustomRange_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Custom date range picker coming soon!", "Feature Preview",
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            CloseSidebar();
        }

        #endregion

        #region Category Filter Methods

        private void FilterByCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string category)
            {
                ThemedMessageBox.Show(this, $"Filtering by {category} category coming soon!", "Feature Preview",
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
                CloseSidebar();
            }
        }

        private void ShowAllCategories_Click(object sender, RoutedEventArgs e)
        {
            RefreshAppList();
            CloseSidebar();
        }

        private void ShowWeeklyReport_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "Weekly report feature coming soon!", "Feature Preview",
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            CloseSidebar();
        }

        #endregion

        private void SetOverlayCheckBoxValue(string name, bool value)
        {
            var control = FindName(name);
            if (control is CheckBox checkBox)
            {
                checkBox.IsChecked = value;
            }
        }

        private void SetOverlayTextBoxValue(string name, string value)
        {
            var control = FindName(name);
            if (control is TextBox textBox)
            {
                textBox.Text = value;
            }
        }

        private bool GetOverlayCheckBoxValue(string name)
        {
            var control = FindName(name);
            if (control is CheckBox checkBox)
            {
                return checkBox.IsChecked == true;
            }
            return false;
        }

        private int GetOverlayIntTextBoxValue(string name, int defaultValue)
        {
            var control = FindName(name);
            if (control is TextBox textBox)
            {
                if (int.TryParse(textBox.Text, out int value) && value > 0)
                {
                    return value;
                }
            }
            return defaultValue;
        }

        private List<string> SaveOverlayUIToWorkingSettings()
        {
            var changedSettings = new List<string>();
            
            try
            {
                if (_workingOverlaySettings == null)
                    _workingOverlaySettings = new AppSettings();

                var oldSettings = _settingsService.CurrentSettings;

                // General Settings
                var newAlwaysOnTop = GetOverlayCheckBoxValue("OverlayAlwaysOnTopCheckBox");
                if (_workingOverlaySettings.AlwaysOnTop != newAlwaysOnTop)
                {
                    _workingOverlaySettings.AlwaysOnTop = newAlwaysOnTop;
                    changedSettings.Add($"Always on top is now {(newAlwaysOnTop ? "enabled" : "disabled")}");
                }

                var newShowInSystemTray = GetOverlayCheckBoxValue("OverlayShowInSystemTrayCheckBox");
                if (_workingOverlaySettings.ShowInSystemTray != newShowInSystemTray)
                {
                    _workingOverlaySettings.ShowInSystemTray = newShowInSystemTray;
                    changedSettings.Add($"Close to tray is now {(newShowInSystemTray ? "enabled" : "disabled")}");
                }

                var newHideTitleBar = GetOverlayCheckBoxValue("OverlayHideTitleBarCheckBox");
                if (_workingOverlaySettings.HideTitleBar != newHideTitleBar)
                {
                    _workingOverlaySettings.HideTitleBar = newHideTitleBar;
                    changedSettings.Add($"Hide title bar is now {(newHideTitleBar ? "enabled" : "disabled")}");
                }
                
                // Break Notifications
                var newEnableBreakNotifications = GetOverlayCheckBoxValue("OverlayEnableBreakNotificationsCheckBox");
                if (_workingOverlaySettings.EnableBreakNotifications != newEnableBreakNotifications)
                {
                    _workingOverlaySettings.EnableBreakNotifications = newEnableBreakNotifications;
                    changedSettings.Add($"Break notifications are now {(newEnableBreakNotifications ? "enabled" : "disabled")}");
                }

                var newBreakReminderMinutes = GetOverlayIntTextBoxValue("OverlayBreakReminderMinutesTextBox", 30);
                if (_workingOverlaySettings.BreakReminderMinutes != newBreakReminderMinutes)
                {
                    _workingOverlaySettings.BreakReminderMinutes = newBreakReminderMinutes;
                    changedSettings.Add($"Break reminder interval changed to {newBreakReminderMinutes} minutes");
                }
                
                // Screen Break Notifications
                var newEnableScreenBreakNotifications = GetOverlayCheckBoxValue("OverlayEnableScreenBreakNotificationsCheckBox");
                if (_workingOverlaySettings.EnableScreenBreakNotifications != newEnableScreenBreakNotifications)
                {
                    _workingOverlaySettings.EnableScreenBreakNotifications = newEnableScreenBreakNotifications;
                    changedSettings.Add($"Screen break notifications are now {(newEnableScreenBreakNotifications ? "enabled" : "disabled")}");
                }

                var newScreenBreakReminderMinutes = GetOverlayIntTextBoxValue("OverlayScreenBreakReminderMinutesTextBox", 20);
                if (_workingOverlaySettings.ScreenBreakReminderMinutes != newScreenBreakReminderMinutes)
                {
                    _workingOverlaySettings.ScreenBreakReminderMinutes = newScreenBreakReminderMinutes;
                    changedSettings.Add($"Screen break interval changed to {newScreenBreakReminderMinutes} minutes");
                }

                var newPlaySoundWithBreakReminder = GetOverlayCheckBoxValue("OverlayPlaySoundWithBreakReminderCheckBox");
                if (_workingOverlaySettings.PlaySoundWithBreakReminder != newPlaySoundWithBreakReminder)
                {
                    _workingOverlaySettings.PlaySoundWithBreakReminder = newPlaySoundWithBreakReminder;
                    changedSettings.Add($"Sound with break reminders is now {(newPlaySoundWithBreakReminder ? "enabled" : "disabled")}");
                }
                
                // Notification Sound
                if (OverlayNotificationSoundComboBox != null && OverlayNotificationSoundComboBox.SelectedItem is string selectedSound)
                {
                    if (_workingOverlaySettings.NotificationSoundFile != selectedSound)
                    {
                        _workingOverlaySettings.NotificationSoundFile = selectedSound;
                        changedSettings.Add($"Notification sound changed to {selectedSound}");
                    }
                }
                
                // Notification Volume
                if (OverlayNotificationVolumeSlider != null)
                {
                    var newVolume = (int)OverlayNotificationVolumeSlider.Value;
                    if (_workingOverlaySettings.NotificationVolume != newVolume)
                    {
                        _workingOverlaySettings.NotificationVolume = newVolume;
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
    }
}