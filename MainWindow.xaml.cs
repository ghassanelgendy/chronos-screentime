using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using chronos_screentime.Models;
using chronos_screentime.Services;
using chronos_screentime.Windows;

namespace chronos_screentime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ScreenTimeService _screenTimeService;
        private readonly DispatcherTimer _uiUpdateTimer;
        private bool _isTracking = false;
        private DateTime _trackingStartTime;

        public MainWindow()
        {
            InitializeComponent();
            
            _screenTimeService = new ScreenTimeService();
            _screenTimeService.DataChanged += OnDataChanged;
            
            // Timer to update UI every second
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiUpdateTimer.Tick += UpdateUI;
            _uiUpdateTimer.Start();
            
            // Initial UI update
            RefreshAppList();
            UpdateStatusUI();
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
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
            
            StartStopButton.Content = "Stop Tracking";
            StartStopButton.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Red
            
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Green
        }

        private void StopTracking()
        {
            _isTracking = false;
            _screenTimeService.StopTracking();
            
            StartStopButton.Content = "Start Tracking";
            StartStopButton.Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Green
            
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Red
            SessionTimeText.Text = "Not tracking";
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

        protected override void OnClosed(EventArgs e)
        {
            _screenTimeService?.Dispose();
            _uiUpdateTimer?.Stop();
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
            ThemedMessageBox.Show(this, "System Tray feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
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
            ThemedMessageBox.Show(this, "Break Notifications feature coming soon!", "Feature Preview", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
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
            var preferencesWindow = new PreferencesWindow();
            preferencesWindow.Owner = this;
            preferencesWindow.ShowDialog();
        }

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

General:
• F5 - Refresh Data
• Ctrl+R - Reset All Data
• Space - Start/Stop Tracking";

            ThemedMessageBox.Show(this, shortcuts, "Keyboard Shortcuts", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void OpenSource_Click(object sender, RoutedEventArgs e)
        {
            ThemedMessageBox.Show(this, "GitHub repository: https://github.com/your-username/chronos-screentime\n\nChronos Screen Time Tracker is open source!", 
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

© 2024 - Built with .NET 8.0 and WPF
Open Source Software";

            ThemedMessageBox.Show(this, about, "About Chronos", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        #endregion
    }
}