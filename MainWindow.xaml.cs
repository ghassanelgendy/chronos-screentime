using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using chronos_screentime.Models;
using chronos_screentime.Services;
using chronos_screentime.Windows;
using Hardcodet.Wpf.TaskbarNotification;
<<<<<<< Updated upstream
using System.ComponentModel;
using System.Collections.Generic;
=======
using System.Runtime.InteropServices;
using Wpf.Ui.Controls;
>>>>>>> Stashed changes

namespace chronos_screentime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly ScreenTimeService _screenTimeService;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly SettingsService _settingsService;
        private readonly BreakNotificationService _breakNotificationService;
        private readonly Services.IDialogService _dialogService;
        private bool _isTracking = false;
        private DateTime _trackingStartTime;
        private string _currentPeriod = "Today";
        
        // System Tray functionality
        private TaskbarIcon? _taskbarIcon;
        private bool _isMinimizeToTrayEnabled = false;
        private bool _isClosingToTray = false;
        private WindowState _previousWindowState = WindowState.Normal;

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
                RefreshControlThemes(this);
                
                // Trigger UI refresh
                OnSystemThemeChanged();
                
                System.Diagnostics.Debug.WriteLine("Theme manually refreshed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing theme: {ex.Message}");
            }
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
                // Use balloon tip notification - more reliable than Windows toast
                _taskbarIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
                System.Diagnostics.Debug.WriteLine($"Break notification shown: {title} - {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing break notification: {ex.Message}");
                
                // Fallback to modal dialog if balloon tip fails
                try
                {
                    var result = ThemedMessageBox.Show(
                        this,
                        message,
                        title,
                        ThemedMessageBox.MessageButtons.OK,
                        ThemedMessageBox.MessageType.Information);
                    System.Diagnostics.Debug.WriteLine("Modal dialog fallback shown");
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Modal dialog fallback also failed: {modalEx.Message}");
                }
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
            
            // Use WPF UI theme color for success state
            StatusIndicator.Fill = (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            
            TrackingStatusPrefix.Text = "Screentime is currently being tracked click to ";
            TrackingActionWord.Text = "stop";
        }

        private void StopTracking()
        {
            _isTracking = false;
            _screenTimeService.StopTracking();
            
            // Use WPF UI theme color for critical state
            StatusIndicator.Fill = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            SessionTimeText.Text = "Not tracking";
            
            TrackingStatusPrefix.Text = "Screentime is currently not being tracked click to ";
            TrackingActionWord.Text = "start tracking";
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
                
                await ShowInfoDialogAsync("Settings Updated",
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
                
                await ShowInfoDialogAsync("Settings Updated",
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
                // Load current settings into page controls
                LoadSettingsToPage();
                
                // Show title bar back button
                if (TitleBarBackButton != null)
                    TitleBarBackButton.Visibility = Visibility.Visible;
                
                // Animate out main content
                var fadeOutStoryboard = (Storyboard)this.Resources["FadeOutDownAnimation"];
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, ScreenTimeContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        ScreenTimeContent.Visibility = Visibility.Collapsed;
                        
                        // Show and animate in preferences content
                        PreferencesContent.Visibility = Visibility.Visible;
                        var fadeInStoryboard = (Storyboard)this.Resources["FadeInUpAnimation"];
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, PreferencesContent);
                            fadeInStoryboard.Begin();
                        }
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    ScreenTimeContent.Visibility = Visibility.Collapsed;
                    PreferencesContent.Visibility = Visibility.Visible;
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
<<<<<<< Updated upstream
                // Animate out
                var hideStoryboard = (Storyboard)this.Resources["HidePreferencesOverlayStoryboard"];
                hideStoryboard.Completed += (s, e) => {
                    PreferencesOverlayBackground.Visibility = Visibility.Collapsed;
                    PreferencesOverlay.Visibility = Visibility.Collapsed;
                };
                hideStoryboard.Begin();
=======
                // Stop any playing sound previews
                StopCurrentSound();
                
                // Hide title bar back button
                if (TitleBarBackButton != null)
                    TitleBarBackButton.Visibility = Visibility.Collapsed;
>>>>>>> Stashed changes
                
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
                
                System.Diagnostics.Debug.WriteLine("Preferences page hidden");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hiding preferences page: {ex.Message}");
            }
        }

        private void LoadSettingsToPage()
        {
            try
            {
<<<<<<< Updated upstream
                var settings = _settingsService.CurrentSettings;
                
                // General Settings
                if (OverlayAlwaysOnTopCheckBox != null)
                    OverlayAlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
                if (OverlayShowInSystemTrayCheckBox != null)
                    OverlayShowInSystemTrayCheckBox.IsChecked = settings.ShowInSystemTray;
                if (OverlayHideTitleBarCheckBox != null)
                    OverlayHideTitleBarCheckBox.IsChecked = settings.HideTitleBar;
                
                // Break Notifications
                if (OverlayEnableBreakNotificationsCheckBox != null)
                    OverlayEnableBreakNotificationsCheckBox.IsChecked = settings.EnableBreakNotifications;
                if (OverlayBreakReminderMinutesTextBox != null)
                    OverlayBreakReminderMinutesTextBox.Text = settings.BreakReminderMinutes.ToString();
                
                // Screen Break Notifications
                if (OverlayEnableScreenBreakNotificationsCheckBox != null)
                    OverlayEnableScreenBreakNotificationsCheckBox.IsChecked = settings.EnableScreenBreakNotifications;
                if (OverlayScreenBreakReminderMinutesTextBox != null)
                    OverlayScreenBreakReminderMinutesTextBox.Text = settings.ScreenBreakReminderMinutes.ToString();
                if (OverlayPlaySoundWithBreakReminderCheckBox != null)
                    OverlayPlaySoundWithBreakReminderCheckBox.IsChecked = settings.PlaySoundWithBreakReminder;
=======
                _isLoadingOverlaySettings = true;
                
                // Create working copy of settings
                _workingOverlaySettings = _settingsService.CurrentSettings.Clone();
                
                // Populate notification sound options
                PopulatePageNotificationSoundComboBox();
                
                // General Settings
                SetPageCheckBoxValue("PageAlwaysOnTopCheckBox", _workingOverlaySettings.AlwaysOnTop);
                SetPageCheckBoxValue("PageShowInSystemTrayCheckBox", _workingOverlaySettings.ShowInSystemTray);
                SetPageCheckBoxValue("PageHideTitleBarCheckBox", _workingOverlaySettings.HideTitleBar);
                
                // Break Notifications
                SetPageCheckBoxValue("PageEnableBreakNotificationsCheckBox", _workingOverlaySettings.EnableBreakNotifications);
                SetPageTextBoxValue("PageBreakReminderMinutesTextBox", _workingOverlaySettings.BreakReminderMinutes.ToString());
                
                // Screen Break Notifications
                SetPageCheckBoxValue("PageEnableScreenBreakNotificationsCheckBox", _workingOverlaySettings.EnableScreenBreakNotifications);
                SetPageTextBoxValue("PageScreenBreakReminderMinutesTextBox", _workingOverlaySettings.ScreenBreakReminderMinutes.ToString());
                SetPageCheckBoxValue("PagePlaySoundWithBreakReminderCheckBox", _workingOverlaySettings.PlaySoundWithBreakReminder);
                
                // Notification sound selection
                if (PageNotificationSoundComboBox != null)
                    PageNotificationSoundComboBox.SelectedItem = _workingOverlaySettings.NotificationSoundFile;
                
                // Notification volume
                if (PageNotificationVolumeSlider != null && PageVolumeValueText != null)
                {
                    PageNotificationVolumeSlider.Value = _workingOverlaySettings.NotificationVolume;
                    PageVolumeValueText.Text = $"{_workingOverlaySettings.NotificationVolume}%";
                }
>>>>>>> Stashed changes
                
                // Theme selection
                SetPageThemeComboBoxValue(_workingOverlaySettings.Theme);
                
                System.Diagnostics.Debug.WriteLine("Settings loaded to page");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings to page: {ex.Message}");
            }
<<<<<<< Updated upstream
=======
            finally
            {
                _isLoadingOverlaySettings = false;
            }
        }

        private void PopulatePageNotificationSoundComboBox()
        {
            try
            {
                if (PageNotificationSoundComboBox != null)
                {
                    string wavDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "wav");
                    if (System.IO.Directory.Exists(wavDir))
                    {
                        var files = System.IO.Directory.GetFiles(wavDir, "*.wav").Select(System.IO.Path.GetFileName).ToList();
                        PageNotificationSoundComboBox.Items.Clear();
                        foreach (var file in files)
                        {
                            PageNotificationSoundComboBox.Items.Add(file);
                        }
                        
                        // Add event handler for selection change to play preview
                        PageNotificationSoundComboBox.SelectionChanged -= PageNotificationSoundComboBox_SelectionChanged;
                        PageNotificationSoundComboBox.SelectionChanged += PageNotificationSoundComboBox_SelectionChanged;
                        
                        // Select current setting or first item
                        var currentSetting = _settingsService.CurrentSettings.NotificationSoundFile;
                        if (!string.IsNullOrEmpty(currentSetting) && files.Contains(currentSetting))
                        {
                            PageNotificationSoundComboBox.SelectedItem = currentSetting;
                        }
                        else if (files.Count > 0)
                        {
                            PageNotificationSoundComboBox.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating page notification sound ComboBox: {ex.Message}");
            }
        }

        private void PageNotificationSoundComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        private void PageNotificationVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // Update the volume percentage display
                if (PageVolumeValueText != null)
                {
                    PageVolumeValueText.Text = $"{(int)e.NewValue}%";
                }
                
                // Only play preview sound if user is actively changing volume (not during initial load)
                if (!_isLoadingOverlaySettings && PageNotificationSoundComboBox?.SelectedItem is string selectedSoundFile)
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
            int volume = PageNotificationVolumeSlider?.Value != null ? (int)PageNotificationVolumeSlider.Value : 50;
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
>>>>>>> Stashed changes
        }

        private void SaveSettingsFromPage()
        {
            try
            {
<<<<<<< Updated upstream
                // Get current settings before changes
                var oldSettings = _settingsService.CurrentSettings;
                var changedSettings = new List<string>();
=======
                // Save UI values to working settings and get list of changes
                var changedSettings = SavePageUIToWorkingSettings();
>>>>>>> Stashed changes
                
                _settingsService.UpdateSettings(settings => {
                    // General Settings
                    if (OverlayAlwaysOnTopCheckBox != null && settings.AlwaysOnTop != (OverlayAlwaysOnTopCheckBox.IsChecked == true))
                    {
                        settings.AlwaysOnTop = OverlayAlwaysOnTopCheckBox.IsChecked == true;
                        changedSettings.Add($"Always on top is now {(settings.AlwaysOnTop ? "enabled" : "disabled")}");
                    }
                    
                    if (OverlayShowInSystemTrayCheckBox != null && settings.ShowInSystemTray != (OverlayShowInSystemTrayCheckBox.IsChecked == true))
                    {
                        settings.ShowInSystemTray = OverlayShowInSystemTrayCheckBox.IsChecked == true;
                        changedSettings.Add($"System tray is now {(settings.ShowInSystemTray ? "enabled" : "disabled")}");
                    }
                    
                    if (OverlayHideTitleBarCheckBox != null && settings.HideTitleBar != (OverlayHideTitleBarCheckBox.IsChecked == true))
                    {
                        settings.HideTitleBar = OverlayHideTitleBarCheckBox.IsChecked == true;
                        changedSettings.Add($"Hide title bar is now {(settings.HideTitleBar ? "enabled" : "disabled")}");
                    }
                    
                    // Break Notifications
                    if (OverlayEnableBreakNotificationsCheckBox != null && settings.EnableBreakNotifications != (OverlayEnableBreakNotificationsCheckBox.IsChecked == true))
                    {
                        settings.EnableBreakNotifications = OverlayEnableBreakNotificationsCheckBox.IsChecked == true;
                        changedSettings.Add($"Break notifications {(settings.EnableBreakNotifications ? "enabled" : "disabled")}");
                    }
                    
                    if (OverlayBreakReminderMinutesTextBox != null && int.TryParse(OverlayBreakReminderMinutesTextBox.Text, out int breakMinutes) && breakMinutes > 0 && settings.BreakReminderMinutes != breakMinutes)
                    {
                        settings.BreakReminderMinutes = breakMinutes;
                        changedSettings.Add($"Break interval set to {breakMinutes} minutes");
                    }
                    
                    // Screen Break Notifications
                    if (OverlayEnableScreenBreakNotificationsCheckBox != null && settings.EnableScreenBreakNotifications != (OverlayEnableScreenBreakNotificationsCheckBox.IsChecked == true))
                    {
                        settings.EnableScreenBreakNotifications = OverlayEnableScreenBreakNotificationsCheckBox.IsChecked == true;
                        changedSettings.Add($"Screen break notifications {(settings.EnableScreenBreakNotifications ? "enabled" : "disabled")}");
                    }
                    
                    if (OverlayScreenBreakReminderMinutesTextBox != null && int.TryParse(OverlayScreenBreakReminderMinutesTextBox.Text, out int screenBreakMinutes) && screenBreakMinutes > 0 && settings.ScreenBreakReminderMinutes != screenBreakMinutes)
                    {
                        settings.ScreenBreakReminderMinutes = screenBreakMinutes;
                        changedSettings.Add($"Screen break interval set to {screenBreakMinutes} minutes");
                    }
                    
                    if (OverlayPlaySoundWithBreakReminderCheckBox != null && settings.PlaySoundWithBreakReminder != (OverlayPlaySoundWithBreakReminderCheckBox.IsChecked == true))
                    {
                        settings.PlaySoundWithBreakReminder = OverlayPlaySoundWithBreakReminderCheckBox.IsChecked == true;
                        changedSettings.Add($"Notification sounds {(settings.PlaySoundWithBreakReminder ? "enabled" : "disabled")}");
                    }
                });
                
                // Apply settings immediately
                ApplySettings(_settingsService.CurrentSettings);
                
                // Show specific save confirmation
                if (changedSettings.Count > 0)
                {
<<<<<<< Updated upstream
                    var message = changedSettings.Count == 1 
                        ? $"✓ {changedSettings[0]}" 
                        : $"✓ {changedSettings.Count} settings updated";
                    ShowSaveConfirmation(message);
=======
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
                    
                    System.Diagnostics.Debug.WriteLine($"Settings saved from page: {changedSettings.Count} changes");
>>>>>>> Stashed changes
                }
                
                System.Diagnostics.Debug.WriteLine("Settings saved from overlay");
            }
            catch (Exception ex)
            {
<<<<<<< Updated upstream
                System.Diagnostics.Debug.WriteLine($"Error saving settings from overlay: {ex.Message}");
=======
                System.Diagnostics.Debug.WriteLine($"Error saving settings from page: {ex.Message}");
                ShowSaveConfirmation("✗ Error saving changes");
                throw;
>>>>>>> Stashed changes
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

        private async void ResetPreferencesToDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = await ShowConfirmationDialogAsync(
                "Reset to Defaults",
                "Are you sure you want to reset all preferences to default values?");

            if (confirmed)
            {
<<<<<<< Updated upstream
                _settingsService.ResetToDefaults();
                LoadSettingsToOverlay();
                ApplySettings(_settingsService.CurrentSettings);
                
                ThemedMessageBox.Show(this, "Preferences have been reset to defaults.", "Reset Complete", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
=======
                try
                {
                    // Reset working settings to defaults
                    _workingOverlaySettings = new AppSettings();
                    
                    // Save to service
                    _settingsService.SaveSettings(_workingOverlaySettings);
                    
                    // Reload page with default settings
                    LoadSettingsToPage();
                    
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
>>>>>>> Stashed changes
            }
        }

        private async void ApplyPreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsFromPage();
                // Save confirmation is now shown by SaveSettingsFromPage method
            }
            catch (Exception ex)
            {
<<<<<<< Updated upstream
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
=======
                await ShowErrorDialogAsync("Error", $"Error applying preferences: {ex.Message}");
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
=======

        #region Page Helper Methods

        private void SetPageCheckBoxValue(string name, bool value)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.ToggleSwitch toggleSwitch)
            {
                toggleSwitch.IsChecked = value;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.IsChecked = value;
            }
        }

        private void SetPageTextBoxValue(string name, string value)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.NumberBox numberBox)
            {
                if (double.TryParse(value, out double numValue))
                {
                    numberBox.Value = numValue;
                }
            }
            else if (control is System.Windows.Controls.TextBox textBox)
            {
                textBox.Text = value;
            }
        }

        private bool GetPageCheckBoxValue(string name)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.ToggleSwitch toggleSwitch)
            {
                return toggleSwitch.IsChecked == true;
            }
            else if (control is CheckBox checkBox)
            {
                return checkBox.IsChecked == true;
            }
            return false;
        }

        private int GetPageIntTextBoxValue(string name, int defaultValue)
        {
            var control = FindName(name);
            if (control is Wpf.Ui.Controls.NumberBox numberBox)
            {
                return (int)(numberBox.Value ?? defaultValue);
            }
            else if (control is System.Windows.Controls.TextBox textBox)
            {
                if (int.TryParse(textBox.Text, out int value) && value > 0)
                {
                    return value;
                }
            }
            return defaultValue;
        }

        private void SetPageThemeComboBoxValue(string theme)
        {
            if (FindName("PageThemeComboBox") is ComboBox themeComboBox)
            {
                string tagToSelect = theme switch
                {
                    "Dark Theme" => "Dark",
                    "Auto (System)" => "Auto",
                    _ => "Light" // Default to Light for "Light Theme" or any other value
                };

                foreach (ComboBoxItem item in themeComboBox.Items)
                {
                    if (item.Tag?.ToString() == tagToSelect)
                    {
                        themeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private string GetPageThemeComboBoxValue()
        {
            if (FindName("PageThemeComboBox") is ComboBox themeComboBox && 
                themeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Tag?.ToString() switch
                {
                    "Dark" => "Dark Theme",
                    "Auto" => "Auto (System)",
                    _ => "Light Theme"
                };
            }
            return "Light Theme"; // Default
        }

        private void ApplyPageThemeChange(string theme)
        {
            try
            {
                var themeToApply = theme switch
                {
                    "Dark Theme" => Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    "Light Theme" => Wpf.Ui.Appearance.ApplicationTheme.Light,
                    "Auto (System)" => Wpf.Ui.Appearance.ApplicationTheme.Unknown,
                    _ => Wpf.Ui.Appearance.ApplicationTheme.Unknown
                };

                System.Diagnostics.Debug.WriteLine($"Applying theme: {theme} -> {themeToApply}");

                // Apply theme to the application globally first
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(themeToApply);
                
                // Apply theme to this specific window
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

                // Force complete UI refresh with delay to ensure theme is applied
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.InvalidateVisual();
                    this.UpdateLayout();
                    RefreshControlThemes(this);
                }), DispatcherPriority.Render);

                System.Diagnostics.Debug.WriteLine($"Theme applied from page: {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme from page: {ex.Message}");
            }
        }

        private void RefreshControlThemes(DependencyObject parent)
        {
            try
            {
                // Recursively refresh all child controls
                int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    // Force refresh of the control
                    if (child is FrameworkElement element)
                    {
                        // Force resource refresh
                        element.Resources.MergedDictionaries.Clear();
                        element.InvalidateVisual();
                        element.UpdateLayout();
                        
                        // Apply theme to specific WPF.UI controls
                        if (child is Wpf.Ui.Controls.NavigationView navView)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(navView);
                        }
                        else if (child is Wpf.Ui.Controls.Card card)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(card);
                        }
                        else if (child is Wpf.Ui.Controls.Button button)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(button);
                        }
                        else if (child is Wpf.Ui.Controls.InfoBar infoBar)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(infoBar);
                        }
                        else if (child is Wpf.Ui.Controls.ToggleSwitch toggleSwitch)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(toggleSwitch);
                        }
                        else if (child is Wpf.Ui.Controls.NumberBox numberBox)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(numberBox);
                        }
                        else if (child is Wpf.Ui.Controls.CardExpander cardExpander)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(cardExpander);
                        }
                        else if (child is Wpf.Ui.Controls.TitleBar titleBar)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(titleBar);
                        }
                    }
                    
                    // Recursively process children
                    RefreshControlThemes(child);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing control themes: {ex.Message}");
            }
        }

        private void PageThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoadingOverlaySettings && sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string newTheme = selectedItem.Tag?.ToString() switch
                {
                    "Dark" => "Dark Theme",
                    "Auto" => "Auto (System)",
                    _ => "Light Theme"
                };

                // Update working settings
                if (_workingOverlaySettings != null)
                {
                    _workingOverlaySettings.Theme = newTheme;
                }
                
                // Apply theme immediately for preview
                ApplyPageThemeChange(newTheme);
            }
        }

        #endregion

        private List<string> SavePageUIToWorkingSettings()
        {
            var changedSettings = new List<string>();
            
            try
            {
                if (_workingOverlaySettings == null)
                    _workingOverlaySettings = new AppSettings();

                var oldSettings = _settingsService.CurrentSettings;

                // General Settings
                var newAlwaysOnTop = GetPageCheckBoxValue("PageAlwaysOnTopCheckBox");
                if (_workingOverlaySettings.AlwaysOnTop != newAlwaysOnTop)
                {
                    _workingOverlaySettings.AlwaysOnTop = newAlwaysOnTop;
                    changedSettings.Add($"Always on top is now {(newAlwaysOnTop ? "enabled" : "disabled")}");
                }

                var newShowInSystemTray = GetPageCheckBoxValue("PageShowInSystemTrayCheckBox");
                if (_workingOverlaySettings.ShowInSystemTray != newShowInSystemTray)
                {
                    _workingOverlaySettings.ShowInSystemTray = newShowInSystemTray;
                    changedSettings.Add($"Close to tray is now {(newShowInSystemTray ? "enabled" : "disabled")}");
                }

                var newHideTitleBar = GetPageCheckBoxValue("PageHideTitleBarCheckBox");
                if (_workingOverlaySettings.HideTitleBar != newHideTitleBar)
                {
                    _workingOverlaySettings.HideTitleBar = newHideTitleBar;
                    changedSettings.Add($"Hide title bar is now {(newHideTitleBar ? "enabled" : "disabled")}");
                }
                
                // Break Notifications
                var newEnableBreakNotifications = GetPageCheckBoxValue("PageEnableBreakNotificationsCheckBox");
                if (_workingOverlaySettings.EnableBreakNotifications != newEnableBreakNotifications)
                {
                    _workingOverlaySettings.EnableBreakNotifications = newEnableBreakNotifications;
                    changedSettings.Add($"Break notifications are now {(newEnableBreakNotifications ? "enabled" : "disabled")}");
                }

                var newBreakReminderMinutes = GetPageIntTextBoxValue("PageBreakReminderMinutesTextBox", 30);
                if (_workingOverlaySettings.BreakReminderMinutes != newBreakReminderMinutes)
                {
                    _workingOverlaySettings.BreakReminderMinutes = newBreakReminderMinutes;
                    changedSettings.Add($"Break reminder interval changed to {newBreakReminderMinutes} minutes");
                }
                
                // Screen Break Notifications
                var newEnableScreenBreakNotifications = GetPageCheckBoxValue("PageEnableScreenBreakNotificationsCheckBox");
                if (_workingOverlaySettings.EnableScreenBreakNotifications != newEnableScreenBreakNotifications)
                {
                    _workingOverlaySettings.EnableScreenBreakNotifications = newEnableScreenBreakNotifications;
                    changedSettings.Add($"Screen break notifications are now {(newEnableScreenBreakNotifications ? "enabled" : "disabled")}");
                }

                var newScreenBreakReminderMinutes = GetPageIntTextBoxValue("PageScreenBreakReminderMinutesTextBox", 20);
                if (_workingOverlaySettings.ScreenBreakReminderMinutes != newScreenBreakReminderMinutes)
                {
                    _workingOverlaySettings.ScreenBreakReminderMinutes = newScreenBreakReminderMinutes;
                    changedSettings.Add($"Screen break interval changed to {newScreenBreakReminderMinutes} minutes");
                }

                var newPlaySoundWithBreakReminder = GetPageCheckBoxValue("PagePlaySoundWithBreakReminderCheckBox");
                if (_workingOverlaySettings.PlaySoundWithBreakReminder != newPlaySoundWithBreakReminder)
                {
                    _workingOverlaySettings.PlaySoundWithBreakReminder = newPlaySoundWithBreakReminder;
                    changedSettings.Add($"Sound with break reminders is now {(newPlaySoundWithBreakReminder ? "enabled" : "disabled")}");
                }
                
                // Notification Sound
                if (PageNotificationSoundComboBox != null && PageNotificationSoundComboBox.SelectedItem is string selectedSound)
                {
                    if (_workingOverlaySettings.NotificationSoundFile != selectedSound)
                    {
                        _workingOverlaySettings.NotificationSoundFile = selectedSound;
                        changedSettings.Add($"Notification sound changed to {selectedSound}");
                    }
                }
                
                // Notification Volume
                if (PageNotificationVolumeSlider != null)
                {
                    var newVolume = (int)PageNotificationVolumeSlider.Value;
                    if (_workingOverlaySettings.NotificationVolume != newVolume)
                    {
                        _workingOverlaySettings.NotificationVolume = newVolume;
                        changedSettings.Add($"Notification volume changed to {newVolume}%");
                    }
                }
                
                // Theme
                var newTheme = GetPageThemeComboBoxValue();
                if (_workingOverlaySettings.Theme != newTheme)
                {
                    _workingOverlaySettings.Theme = newTheme;
                    changedSettings.Add($"Theme changed to {newTheme}");
                }
                
                System.Diagnostics.Debug.WriteLine("Page UI saved to working settings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving page UI to working settings: {ex.Message}");
            }

            return changedSettings;
        }
>>>>>>> Stashed changes
    }
}