using chronos_screentime.Models;
using chronos_screentime.Services;
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using TextBlock = System.Windows.Controls.TextBlock;
using System.Linq;

namespace chronos_screentime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        #region Fields
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);

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
        private TaskbarIcon? _taskbarIcon;
        private bool _isMinimizeToTrayEnabled = false;
        private bool _isClosingToTray = false;
        private WindowState _previousWindowState = WindowState.Normal;
        #endregion

        #region Constructor and Initialization
        public MainWindow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Starting initialization...");
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Calling InitializeComponent...");
            InitializeComponent();
                System.Diagnostics.Debug.WriteLine("MainWindow: InitializeComponent completed");

            // Initialize dialog service
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing dialog service...");
            _dialogService = new Services.DialogService();
                System.Diagnostics.Debug.WriteLine("MainWindow: Dialog service initialized");

            // Initialize settings service first
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing settings service...");
            _settingsService = new SettingsService();
                System.Diagnostics.Debug.WriteLine("MainWindow: Settings service initialized");

            // Apply saved theme or default to system detection
                System.Diagnostics.Debug.WriteLine("MainWindow: Applying saved theme...");
            ApplySavedTheme(_settingsService.CurrentSettings.Theme);
                System.Diagnostics.Debug.WriteLine("MainWindow: Theme applied");

            // Ensure theme is properly applied after UI loads
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting up Loaded event handler...");
            this.Loaded += (s, e) =>
            {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("MainWindow: Window Loaded event triggered");
                        System.Diagnostics.Debug.WriteLine("MainWindow: Calling RefreshTheme...");
                RefreshTheme();
                        System.Diagnostics.Debug.WriteLine("MainWindow: RefreshTheme completed in Loaded event");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MainWindow: ERROR in Loaded event: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                        MessageBox.Show($"Error during window loading: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        throw;
                    }
            };

            // Set responsive window size based on screen resolution
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting responsive window size...");
            SetResponsiveWindowSize();
                System.Diagnostics.Debug.WriteLine("MainWindow: Window size set");

                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing screen time service...");
            _screenTimeService = new ScreenTimeService();
                _screenTimeService.DataChanged += OnDataChanged!;
                System.Diagnostics.Debug.WriteLine("MainWindow: Screen time service initialized");

            // Initialize system tray functionality first
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing system tray...");
            InitializeSystemTray();
                System.Diagnostics.Debug.WriteLine("MainWindow: System tray initialized");

            // Initialize break notification service with notification callback
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing break notification service...");
            _breakNotificationService = new BreakNotificationService(_settingsService, ShowBreakNotification, () =>
            {
                // Return true if window is minimized/hidden to tray
                return !this.IsVisible || this.WindowState == WindowState.Minimized;
            });
                System.Diagnostics.Debug.WriteLine("MainWindow: Break notification service initialized");

            // Apply initial settings
                System.Diagnostics.Debug.WriteLine("MainWindow: Applying initial settings...");
            ApplySettings(_settingsService.CurrentSettings);
                System.Diagnostics.Debug.WriteLine("MainWindow: Initial settings applied");

            // Timer to update UI every second
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting up UI update timer...");
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiUpdateTimer.Tick += UpdateUI;
            _uiUpdateTimer.Start();
                System.Diagnostics.Debug.WriteLine("MainWindow: UI update timer started");

            // Refresh data when window gains focus
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting up activation handler...");
            this.Activated += MainWindow_Activated;
                System.Diagnostics.Debug.WriteLine("MainWindow: Activation handler set");

            // Start tracking by default
                System.Diagnostics.Debug.WriteLine("MainWindow: Starting tracking...");
            StartTracking();
                System.Diagnostics.Debug.WriteLine("MainWindow: Tracking started");

            // Initial UI update
                System.Diagnostics.Debug.WriteLine("MainWindow: Performing initial UI updates...");
            RefreshAppList();
            UpdateStatusUI();
                System.Diagnostics.Debug.WriteLine("MainWindow: Initial UI updates completed");

            // Subscribe to window state change events
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting up window state handlers...");
            this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;
                System.Diagnostics.Debug.WriteLine("MainWindow: Window state handlers set");

                System.Diagnostics.Debug.WriteLine("MainWindow: Initialization completed successfully");
                }
                catch (Exception ex)
                {
                System.Diagnostics.Debug.WriteLine($"MainWindow: FATAL ERROR during initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error initializing main window: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
                }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: OnSourceInitialized starting...");
                base.OnSourceInitialized(e);
                System.Diagnostics.Debug.WriteLine("MainWindow: Base OnSourceInitialized completed");

                // Additional initialization that requires window handle
                System.Diagnostics.Debug.WriteLine("MainWindow: Performing post-source initialization...");
                
                // Force layout update
                System.Diagnostics.Debug.WriteLine("MainWindow: Updating layout...");
                this.UpdateLayout();
                System.Diagnostics.Debug.WriteLine("MainWindow: Layout updated");

                System.Diagnostics.Debug.WriteLine("MainWindow: OnSourceInitialized completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: ERROR in OnSourceInitialized: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error during window initialization: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Window Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public new void Show()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Show method called...");
                base.Show();
                System.Diagnostics.Debug.WriteLine("MainWindow: Base Show completed");

                // Force layout update after showing
                System.Diagnostics.Debug.WriteLine("MainWindow: Updating layout after Show...");
                this.UpdateLayout();
                System.Diagnostics.Debug.WriteLine("MainWindow: Post-show layout updated");
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Show completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: ERROR in Show: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error showing window: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Show Window Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

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
        #endregion

        #region Theme Management
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

                // Apply Windows accent color automatically
                ApplyWindowsAccentColor();

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
                    ApplyWindowsAccentColor();
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback theme application also failed: {fallbackEx.Message}");
                }
            }
        }

        private void ApplyWindowsAccentColor()
        {
            try
            {
                // Apply Windows system accent color automatically
                // WPF UI will automatically detect and apply the Windows accent color
                Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent();
                
                System.Diagnostics.Debug.WriteLine("Windows accent color applied successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying Windows accent color: {ex.Message}");
                // If that fails, try the manual approach with a fallback color
                try
                {
                    // Get system accent color from Windows registry or use a nice blue as fallback
                    var accentColor = GetWindowsAccentColor();
                    Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
                        accentColor,
                        Wpf.Ui.Appearance.ApplicationTheme.Unknown
                    );
                    System.Diagnostics.Debug.WriteLine("Applied manual accent color as fallback");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback accent color application failed: {fallbackEx.Message}");
                }
            }
        }

        private System.Windows.Media.Color GetWindowsAccentColor()
        {
            try
            {
                // Try to get Windows 10/11 accent color from registry
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\DWM"))
                {
                    if (key?.GetValue("AccentColor") is int accentColorDword)
                    {
                        var bytes = BitConverter.GetBytes(accentColorDword);
                        return System.Windows.Media.Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2]);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Windows accent color from registry: {ex.Message}");
            }

            // Fallback to a nice blue color
            return System.Windows.Media.Color.FromRgb(0, 120, 215); // Windows Blue
        }

        public void RefreshTheme()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: RefreshTheme starting...");
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Getting current theme...");
                var currentTheme = _settingsService.CurrentSettings.Theme;
                System.Diagnostics.Debug.WriteLine($"MainWindow: Current theme is: {currentTheme}");

                var themeToApply = currentTheme switch
                {
                    "Dark Theme" => Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    "Light Theme" => Wpf.Ui.Appearance.ApplicationTheme.Light,
                    _ => Wpf.Ui.Appearance.ApplicationTheme.Unknown
                };

                System.Diagnostics.Debug.WriteLine($"MainWindow: Applying theme {themeToApply}...");
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(themeToApply);
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Applying theme to window...");
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Applying Windows accent color...");
                ApplyWindowsAccentColor();
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Refreshing control themes...");
                RefreshControlThemes();
                
                System.Diagnostics.Debug.WriteLine("MainWindow: RefreshTheme completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: ERROR in RefreshTheme: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error refreshing theme: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void RefreshControlThemes()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: RefreshControlThemes starting...");
                
                // Force theme refresh on specific controls if needed
                System.Diagnostics.Debug.WriteLine("MainWindow: Refreshing navigation view...");
                if (MainNavigationView != null)
                {
                    MainNavigationView.UpdateLayout();
                }
                
                System.Diagnostics.Debug.WriteLine("MainWindow: RefreshControlThemes completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: ERROR in RefreshControlThemes: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        #endregion

        #region Dialog Helpers
        private async Task<Wpf.Ui.Controls.ContentDialogResult> ShowContentDialogAsync(
            string title,
            string content,
            string primaryButtonText = "OK",
            string? secondaryButtonText = null,
            string? closeButtonText = null)
        {
            return await _dialogService.ShowContentDialogAsync(title, content, primaryButtonText, secondaryButtonText, closeButtonText);
        }

        private async Task ShowInfoDialogAsync(string title, string message)
        {
            await _dialogService.ShowInfoDialogAsync(title, message);
        }

        private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            return await _dialogService.ShowConfirmationDialogAsync(title, message);
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            await _dialogService.ShowErrorDialogAsync(title, message);
        }
        #endregion

        #region Window Management
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

        private void ExitApplication()
        {
            _isClosingToTray = false; // Allow actual closing
            Application.Current.Shutdown();
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
        #endregion

        #region Settings Management
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

        private void LoadSettings()
        {
            try
            {
                                    if (_settingsService == null)
                {
                    throw new InvalidOperationException("Settings service is not initialized");
                }

                var settings = _settingsService.LoadSettings();
                
                // Apply settings to UI elements with null checks
                if (AlwaysOnTopMenuItem != null)
                    AlwaysOnTopMenuItem.IsChecked = settings.AlwaysOnTop;
                
                if (ShowInTrayMenuItem != null)
                    ShowInTrayMenuItem.IsChecked = settings.ShowInSystemTray;
                
                if (HideTitleBarMenuItem != null)
                    HideTitleBarMenuItem.IsChecked = settings.HideTitleBar;

                System.Diagnostics.Debug.WriteLine("========== Settings loaded successfully ==========");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"========== Error loading settings: {ex.Message} ==========");
                MessageBox.Show($"Error loading settings: {ex.Message}", 
                              "Settings Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Warning);
            }
        }

        private void SaveSettingsFromOverlay()
        {
            try
            {
                _settingsService?.UpdateSettings(s =>
                {
                    if (s != null)
                    {
                        s.AlwaysOnTop = this.Topmost;
                        s.ShowInSystemTray = _isMinimizeToTrayEnabled;
                        s.HideTitleBar = this.ExtendsContentIntoTitleBar;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void ShowSaveConfirmation(string message = "✓ Changes saved")
        {
            // Use WPF UI's Snackbar or InfoBar for modern notifications
            var infoBar = new Wpf.Ui.Controls.InfoBar
            {
                Title = "Settings Saved",
                Message = message,
                Severity = Wpf.Ui.Controls.InfoBarSeverity.Success,
                IsOpen = true,
                IsClosable = true
            };

            // Find the settings content grid
            if (PreferencesContent.FindName("SettingsDetailContent") is ScrollViewer scrollViewer &&
                scrollViewer.Content is StackPanel stackPanel)
            {
                // Insert the InfoBar at the top
                stackPanel.Children.Insert(0, infoBar);

                // Auto-close after 3 seconds
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (s, e) =>
                {
                    stackPanel.Children.Remove(infoBar);
                    timer.Stop();
                };
                timer.Start();
            }
        }
        #endregion

        #region Tracking Management
        private void StartTracking()
        {
            _isTracking = true;
            _trackingStartTime = DateTime.Now;
            _screenTimeService.StartTracking();
            UpdateUI(this, EventArgs.Empty);
        }

        private void StopTracking()
        {
            _isTracking = false;
            _screenTimeService.StopTracking();
            UpdateUI(this, EventArgs.Empty);
        }

        private void OnDataChanged(object? sender, EventArgs e)
        {
            if (this.Dispatcher != null)
            {
                this.Dispatcher.Invoke(() =>
                {
                    try
                    {
                RefreshAppList();
            }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error refreshing app list: {ex.Message}");
                    }
                });
            }
        }

        private void RefreshAppList()
        {
            var today = DateTime.Today;
            var data = _screenTimeService.GetScreenTimeData();
            var year = today.Year;
            var month = today.Month;
            var currentWeek = GetIso8601WeekOfYear(today);

            List<AppDailyData> apps = new();

            switch (_currentPeriod)
            {
                case "Today":
                    if (data.Years.TryGetValue(year, out var yearData) &&
                        yearData.Months.TryGetValue(month, out var monthData) &&
                        monthData.Weeks.TryGetValue(currentWeek, out var weekData) &&
                        weekData.Days.TryGetValue(today, out var dayData))
                    {
                        apps = dayData.Apps.Values.ToList();
                    }
                    break;

                case "Yesterday":
                    var yesterday = today.AddDays(-1);
                    if (data.Years.TryGetValue(yesterday.Year, out yearData) &&
                        yearData.Months.TryGetValue(yesterday.Month, out monthData) &&
                        monthData.Weeks.TryGetValue(GetIso8601WeekOfYear(yesterday), out weekData) &&
                        weekData.Days.TryGetValue(yesterday, out dayData))
                    {
                        apps = dayData.Apps.Values.ToList();
                    }
                    break;

                case "This Week":
                    if (data.Years.TryGetValue(year, out yearData) &&
                        yearData.Months.TryGetValue(month, out monthData) &&
                        monthData.Weeks.TryGetValue(currentWeek, out weekData))
                    {
                        // Aggregate app data for the week
                        var weekApps = new Dictionary<string, AppDailyData>();
                        foreach (var day in weekData.Days.Values)
                        {
                            foreach (var app in day.Apps.Values)
                            {
                                if (!weekApps.ContainsKey(app.AppName))
                                {
                                    weekApps[app.AppName] = new AppDailyData
                                    {
                                        AppName = app.AppName,
                                        ProcessPath = app.ProcessPath,
                                        FirstSeen = app.FirstSeen,
                                        LastSeen = app.LastSeen,
                                        LastActiveTime = app.LastActiveTime
                                    };
                                }
                                var weekApp = weekApps[app.AppName];
                                weekApp.TotalTime += app.TotalTime;
                                weekApp.SessionCount += app.SessionCount;
                                if (app.FirstSeen < weekApp.FirstSeen) weekApp.FirstSeen = app.FirstSeen;
                                if (app.LastSeen > weekApp.LastSeen) weekApp.LastSeen = app.LastSeen;
                                if (app.LastActiveTime > weekApp.LastActiveTime) weekApp.LastActiveTime = app.LastActiveTime;
                            }
                        }
                        apps = weekApps.Values.ToList();
                    }
                    break;

                case "Last Week":
                    var lastWeek = today.AddDays(-7);
                    var lastWeekNumber = GetIso8601WeekOfYear(lastWeek);
                    if (data.Years.TryGetValue(lastWeek.Year, out yearData) &&
                        yearData.Months.TryGetValue(lastWeek.Month, out monthData) &&
                        monthData.Weeks.TryGetValue(lastWeekNumber, out weekData))
                    {
                        // Aggregate app data for last week
                        var weekApps = new Dictionary<string, AppDailyData>();
                        foreach (var day in weekData.Days.Values)
                        {
                            foreach (var app in day.Apps.Values)
                            {
                                if (!weekApps.ContainsKey(app.AppName))
                                {
                                    weekApps[app.AppName] = new AppDailyData
                                    {
                                        AppName = app.AppName,
                                        ProcessPath = app.ProcessPath,
                                        FirstSeen = app.FirstSeen,
                                        LastSeen = app.LastSeen,
                                        LastActiveTime = app.LastActiveTime
                                    };
                                }
                                var weekApp = weekApps[app.AppName];
                                weekApp.TotalTime += app.TotalTime;
                                weekApp.SessionCount += app.SessionCount;
                                if (app.FirstSeen < weekApp.FirstSeen) weekApp.FirstSeen = app.FirstSeen;
                                if (app.LastSeen > weekApp.LastSeen) weekApp.LastSeen = app.LastSeen;
                                if (app.LastActiveTime > weekApp.LastActiveTime) weekApp.LastActiveTime = app.LastActiveTime;
                            }
                        }
                        apps = weekApps.Values.ToList();
                    }
                    break;

                case "This Month":
                    if (data.Years.TryGetValue(year, out yearData) &&
                        yearData.Months.TryGetValue(month, out monthData))
                    {
                        // Aggregate app data for the month
                        var monthApps = new Dictionary<string, AppDailyData>();
                        foreach (var currentWeekData in monthData.Weeks.Values)
                        {
                            foreach (var day in currentWeekData.Days.Values)
                            {
                                foreach (var app in day.Apps.Values)
                                {
                                    if (!monthApps.ContainsKey(app.AppName))
                                    {
                                        monthApps[app.AppName] = new AppDailyData
                                        {
                                            AppName = app.AppName,
                                            ProcessPath = app.ProcessPath,
                                            FirstSeen = app.FirstSeen,
                                            LastSeen = app.LastSeen,
                                            LastActiveTime = app.LastActiveTime
                                        };
                                    }
                                    var monthApp = monthApps[app.AppName];
                                    monthApp.TotalTime += app.TotalTime;
                                    monthApp.SessionCount += app.SessionCount;
                                    if (app.FirstSeen < monthApp.FirstSeen) monthApp.FirstSeen = app.FirstSeen;
                                    if (app.LastSeen > monthApp.LastSeen) monthApp.LastSeen = app.LastSeen;
                                    if (app.LastActiveTime > monthApp.LastActiveTime) monthApp.LastActiveTime = app.LastActiveTime;
                                }
                            }
                        }
                        apps = monthApps.Values.ToList();
                    }
                    break;
            }

            AppListView.ItemsSource = apps.OrderByDescending(a => a.TotalTime.TotalMilliseconds);
            UpdateSummaryUI(apps);
            UpdateNavigationStats();
        }

        private void UpdateUI(object? sender, EventArgs? e)
        {
            try
            {
                if (this.Dispatcher != null)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        UpdateStatusUI();
                        UpdateTrayTooltip();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating UI: {ex.Message}");
            }
        }

        private void UpdateStatusUI()
        {
            if (StatusIndicator == null || SessionTimeText == null) return;

            StatusIndicator.Fill = _isTracking ? 
                new SolidColorBrush(Color.FromRgb(46, 204, 113)) : 
                new SolidColorBrush(Color.FromRgb(231, 76, 60));

            SessionTimeText.Text = _isTracking ? 
                DateTime.Now.Subtract(_trackingStartTime).ToString(@"hh\:mm\:ss") : 
                "Not tracking";
        }

        private void UpdateTrayTooltip()
        {
            if (_taskbarIcon != null)
            {
                try
                {
                    var status = _isTracking ? "Tracking" : "Not tracking";
                    var time = _isTracking ? DateTime.Now.Subtract(_trackingStartTime).ToString(@"hh\:mm\:ss") : "00:00:00";
                    _taskbarIcon.ToolTipText = $"Chronos Screen Time Tracker\nStatus: {status}\nSession time: {time}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating tray tooltip: {ex.Message}");
                }
            }
        }

        private void UpdateNavigationStats()
        {
            var today = DateTime.Today;
            var data = _screenTimeService.GetScreenTimeData();
            var year = today.Year;
            var month = today.Month;
            var currentWeek = GetIso8601WeekOfYear(today);

            // Update sidebar stats based on current period
            if (SidebarCurrentPeriod != null)
                SidebarCurrentPeriod.Text = _currentPeriod;

            if (SidebarTotalTime != null)
            {
                TimeSpan totalTime = TimeSpan.Zero;
                int totalSwitches = 0;
                int totalApps = 0;

                switch (_currentPeriod)
                {
                    case "Today":
                        if (data.Years.TryGetValue(year, out var yearData) &&
                            yearData.Months.TryGetValue(month, out var monthData) &&
                            monthData.Weeks.TryGetValue(currentWeek, out var weekData) &&
                            weekData.Days.TryGetValue(today, out var dayData))
                        {
                            totalTime = dayData.TotalTime;
                            totalSwitches = dayData.TotalSwitches;
                            totalApps = dayData.TotalApps;
                        }
                        break;

                    case "Yesterday":
                        var yesterday = today.AddDays(-1);
                        if (data.Years.TryGetValue(yesterday.Year, out yearData) &&
                            yearData.Months.TryGetValue(yesterday.Month, out monthData) &&
                            monthData.Weeks.TryGetValue(GetIso8601WeekOfYear(yesterday), out weekData) &&
                            weekData.Days.TryGetValue(yesterday, out dayData))
                        {
                            totalTime = dayData.TotalTime;
                            totalSwitches = dayData.TotalSwitches;
                            totalApps = dayData.TotalApps;
                        }
                        break;

                    case "This Week":
                        if (data.Years.TryGetValue(year, out yearData) &&
                            yearData.Months.TryGetValue(month, out monthData) &&
                            monthData.Weeks.TryGetValue(currentWeek, out weekData))
                        {
                            totalTime = weekData.TotalTime;
                            totalSwitches = weekData.TotalSwitches;
                            totalApps = weekData.TotalApps;
                        }
                        break;

                    case "Last Week":
                        var lastWeek = today.AddDays(-7);
                        var lastWeekNumber = GetIso8601WeekOfYear(lastWeek);
                        if (data.Years.TryGetValue(lastWeek.Year, out yearData) &&
                            yearData.Months.TryGetValue(lastWeek.Month, out monthData) &&
                            monthData.Weeks.TryGetValue(lastWeekNumber, out weekData))
                        {
                            totalTime = weekData.TotalTime;
                            totalSwitches = weekData.TotalSwitches;
                            totalApps = weekData.TotalApps;
                        }
                        break;

                    case "This Month":
                        if (data.Years.TryGetValue(year, out yearData) &&
                            yearData.Months.TryGetValue(month, out monthData))
                        {
                            totalTime = monthData.TotalTime;
                            totalSwitches = monthData.TotalSwitches;
                            totalApps = monthData.TotalApps;
                        }
                        break;
                }

                var hours = (int)totalTime.TotalHours;
                var minutes = totalTime.Minutes;
                SidebarTotalTime.Text = $"{hours}h {minutes}m";
                SidebarSwitches.Text = $"{totalSwitches} switches";
                SidebarApps.Text = $"{totalApps} apps";
            }
        }

        private void UpdateSummaryUI(System.Collections.Generic.List<AppDailyData> apps)
        {
            TotalAppsText.Text = apps.Count.ToString();

            var totalTime = apps.Sum(a => a.TotalTime.TotalMilliseconds);
            var totalTimeSpan = TimeSpan.FromMilliseconds(totalTime);
            var hours = (int)totalTimeSpan.TotalHours;
            var minutes = totalTimeSpan.Minutes;
            TotalTimeText.Text = $"{hours}h {minutes}m";

            var totalSwitches = apps.Sum(a => a.SessionCount);
            TotalSwitchesText.Text = totalSwitches.ToString();
        }
        #endregion

        #region Sound Management
        private void PopulatePageNotificationSoundComboBox()
        {
            try
            {
                if (PageNotificationSoundComboBox != null)
                {
                    PageNotificationSoundComboBox.Items.Clear();

                    var soundFiles = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "wav"), "*.wav");
                    foreach (var soundFile in soundFiles)
                    {
                        var fileName = Path.GetFileName(soundFile); // Keep full filename with .wav
                        var displayName = Path.GetFileNameWithoutExtension(soundFile); // Display name without extension
                        var item = new System.Windows.Controls.ComboBoxItem
                        {
                            Content = displayName,
                            Tag = fileName // Store full filename including .wav
                        };
                        PageNotificationSoundComboBox.Items.Add(item);
                    }

                    if (PageNotificationSoundComboBox.Items.Count > 0)
                    {
                        // Set default sound or load from settings
                        var savedSound = _settingsService?.CurrentSettings?.NotificationSoundFile;
                        if (!string.IsNullOrEmpty(savedSound))
                        {
                            var savedItem = PageNotificationSoundComboBox.Items.Cast<ComboBoxItem>()
                                .FirstOrDefault(item => item.Tag?.ToString() == savedSound);
                            if (savedItem != null)
                            {
                                PageNotificationSoundComboBox.SelectedItem = savedItem;
                            }
                            else
                            {
                                PageNotificationSoundComboBox.SelectedIndex = 0;
                            }
                        }
                        else
                        {
                            PageNotificationSoundComboBox.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating notification sound combo box: {ex.Message}");
            }
        }

        private async Task ShowToastNotificationWithSound(string title, string message, string? soundFileName = null, int volume = 50)
        {
            try
            {
                // Show toast notification first
                if (_taskbarIcon != null)
                {
                    _taskbarIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
                    System.Diagnostics.Debug.WriteLine($"Toast notification shown: {title} - {message}");
                }

                // Wait for 3 seconds
                await Task.Delay(3000);

                // Play sound after delay
                if (!string.IsNullOrEmpty(soundFileName))
                {
                    var soundPath = Path.Combine("assets", "wav", $"{soundFileName}.wav");
                    if (File.Exists(soundPath))
                    {
                        PlaySoundWithVolumeControl(soundPath, volume);
                        System.Diagnostics.Debug.WriteLine($"Playing notification sound: {soundFileName} at {volume}% volume");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Sound file not found: {soundPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in toast notification with sound: {ex.Message}");
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
        #endregion

        #region Event Handlers
        private void TrackingStatusFooter_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var textBlock = sender as System.Windows.Controls.TextBlock;
            if (textBlock == null)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Warning - Sender is not a TextBlock");
                return;
            }

            if (_isTracking)
            {
                StopTracking();
                textBlock.Text = "start";
            }
            else
            {
                StartTracking();
                textBlock.Text = "stop";
            }
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

        private void ShowPreferences_Click(object sender, RoutedEventArgs e)
        {
            ShowPreferencesPage();
        }

        private void BackToMain_Click(object sender, RoutedEventArgs e)
        {
            HidePreferencesPage();
        }

        private void PageNotificationSoundComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Only play preview if user is actively selecting (not during initial load)
                if (!_isLoadingPageSettings && sender is ComboBox comboBox && 
                    comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
                {
                    string? soundFileName = selectedItem.Tag?.ToString();
                    if (!string.IsNullOrEmpty(soundFileName))
                    {
                        // Save the sound setting
                        _settingsService?.UpdateSettings(s => s.NotificationSoundFile = soundFileName);
                        
                        // Play preview
                        PlaySoundPreview(soundFileName);
                    }
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
                
                // Save the volume setting
                if (!_isLoadingPageSettings)
                {
                    _settingsService?.UpdateSettings(s => s.NotificationVolume = (int)e.NewValue);
                }
                
                // Only play preview sound if user is actively changing volume (not during initial load)
                if (!_isLoadingPageSettings && PageNotificationSoundComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
                {
                    string? soundFileName = selectedItem.Tag?.ToString();
                    if (!string.IsNullOrEmpty(soundFileName))
                    {
                        PlaySoundPreviewWithVolume(soundFileName, (int)e.NewValue);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling volume slider change: {ex.Message}");
            }
        }

        private void ApplyPreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Applying preferences...");
                
                // Collect all settings from UI controls
                var newSettings = new AppSettings();
                
                // System settings
                if (PageAlwaysOnTopCheckBox != null)
                    newSettings.AlwaysOnTop = PageAlwaysOnTopCheckBox.IsChecked ?? false;
                
                if (PageShowInSystemTrayCheckBox != null)
                    newSettings.ShowInSystemTray = PageShowInSystemTrayCheckBox.IsChecked ?? false;
                
                if (PageHideTitleBarCheckBox != null)
                    newSettings.HideTitleBar = PageHideTitleBarCheckBox.IsChecked ?? false;

                // Theme setting
                if (PageThemeComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem themeItem)
                {
                    var themeTag = themeItem.Tag?.ToString();
                    newSettings.Theme = themeTag switch
                    {
                        "Light" => "Light Theme",
                        "Dark" => "Dark Theme",
                        "Auto" => "Auto (System)",
                        _ => "Auto (System)"
                    };
                }

                // Break notification settings
                if (PageEnableBreakNotificationsCheckBox != null)
                    newSettings.EnableBreakNotifications = PageEnableBreakNotificationsCheckBox.IsChecked ?? false;
                
                if (PageBreakReminderMinutesTextBox != null)
                    newSettings.BreakReminderMinutes = (int)(PageBreakReminderMinutesTextBox.Value > 0 ? PageBreakReminderMinutesTextBox.Value : 30);

                // Screen break notification settings
                if (PageEnableScreenBreakNotificationsCheckBox != null)
                    newSettings.EnableScreenBreakNotifications = PageEnableScreenBreakNotificationsCheckBox.IsChecked ?? false;
                
                if (PageScreenBreakReminderMinutesTextBox != null)
                    newSettings.ScreenBreakReminderMinutes = (int)(PageScreenBreakReminderMinutesTextBox.Value > 0 ? PageScreenBreakReminderMinutesTextBox.Value : 20);
                
                if (PagePlaySoundWithBreakReminderCheckBox != null)
                    newSettings.PlaySoundWithBreakReminder = PagePlaySoundWithBreakReminderCheckBox.IsChecked ?? false;

                // Sound settings
                if (PageNotificationSoundComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem soundItem)
                {
                    newSettings.NotificationSoundFile = soundItem.Tag?.ToString() ?? "Beep.wav";
                }
                
                if (PageNotificationVolumeSlider != null)
                    newSettings.NotificationVolume = (int)PageNotificationVolumeSlider.Value;

                // Apply settings to the settings service (this writes to JSON)
                _settingsService?.UpdateSettings(s =>
                {
                    s.AlwaysOnTop = newSettings.AlwaysOnTop;
                    s.ShowInSystemTray = newSettings.ShowInSystemTray;
                    s.HideTitleBar = newSettings.HideTitleBar;
                    s.Theme = newSettings.Theme;
                    s.EnableBreakNotifications = newSettings.EnableBreakNotifications;
                    s.BreakReminderMinutes = newSettings.BreakReminderMinutes;
                    s.EnableScreenBreakNotifications = newSettings.EnableScreenBreakNotifications;
                    s.ScreenBreakReminderMinutes = newSettings.ScreenBreakReminderMinutes;
                    s.PlaySoundWithBreakReminder = newSettings.PlaySoundWithBreakReminder;
                    s.NotificationSoundFile = newSettings.NotificationSoundFile;
                    s.NotificationVolume = newSettings.NotificationVolume;
                });

                // Apply window-level settings immediately
                this.Topmost = newSettings.AlwaysOnTop;
                _isMinimizeToTrayEnabled = newSettings.ShowInSystemTray;
                this.ExtendsContentIntoTitleBar = newSettings.HideTitleBar;

                // Apply theme
                ApplySavedTheme(newSettings.Theme);

                // Update menu checkboxes
                if (AlwaysOnTopMenuItem != null)
                    AlwaysOnTopMenuItem.IsChecked = newSettings.AlwaysOnTop;
                if (ShowInTrayMenuItem != null)
                    ShowInTrayMenuItem.IsChecked = newSettings.ShowInSystemTray;
                if (HideTitleBarMenuItem != null)
                    HideTitleBarMenuItem.IsChecked = newSettings.HideTitleBar;

                // Update tray icon visibility
                if (_taskbarIcon != null)
                {
                    _taskbarIcon.Visibility = newSettings.ShowInSystemTray ? Visibility.Visible : Visibility.Collapsed;
                }

                // Force settings to save immediately
                _settingsService?.SaveSettings();

                System.Diagnostics.Debug.WriteLine("MainWindow: All preferences applied and saved successfully");
                
                // Show success message
                ShowSaveConfirmation("✓ Settings saved successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error applying preferences: {ex.Message}");
                MessageBox.Show($"Failed to apply preferences: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            MainNavigationView.IsPaneOpen = !MainNavigationView.IsPaneOpen;
        }

        private void ShowPreferencesPage()
        {
            try
            {
                var preferencesContent = PreferencesContent;
                var screenTimeContent = ScreenTimeContent;

                if (preferencesContent == null || screenTimeContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - PreferencesContent or ScreenTimeContent is null");
                    return;
                }

                // Set loading flag to prevent sound previews during initial load
                _isLoadingPageSettings = true;

                // Get current settings
                var currentSettings = _settingsService?.CurrentSettings ?? new AppSettings();

                // Load system settings
                if (PageAlwaysOnTopCheckBox != null)
                    PageAlwaysOnTopCheckBox.IsChecked = currentSettings.AlwaysOnTop;
                
                if (PageShowInSystemTrayCheckBox != null)
                    PageShowInSystemTrayCheckBox.IsChecked = currentSettings.ShowInSystemTray;
                
                if (PageHideTitleBarCheckBox != null)
                    PageHideTitleBarCheckBox.IsChecked = currentSettings.HideTitleBar;

                // Load theme setting
                if (PageThemeComboBox != null)
                {
                    var themeTag = currentSettings.Theme switch
                    {
                        "Light Theme" => "Light",
                        "Dark Theme" => "Dark",
                        "Auto (System)" => "Auto",
                        _ => "Auto"
                    };
                    
                    var themeItem = PageThemeComboBox.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == themeTag);
                    if (themeItem != null)
                        PageThemeComboBox.SelectedItem = themeItem;
                }

                // Load break notification settings
                if (PageEnableBreakNotificationsCheckBox != null)
                    PageEnableBreakNotificationsCheckBox.IsChecked = currentSettings.EnableBreakNotifications;
                
                if (PageBreakReminderMinutesTextBox != null)
                    PageBreakReminderMinutesTextBox.Value = currentSettings.BreakReminderMinutes;

                // Load screen break notification settings
                if (PageEnableScreenBreakNotificationsCheckBox != null)
                    PageEnableScreenBreakNotificationsCheckBox.IsChecked = currentSettings.EnableScreenBreakNotifications;
                
                if (PageScreenBreakReminderMinutesTextBox != null)
                    PageScreenBreakReminderMinutesTextBox.Value = currentSettings.ScreenBreakReminderMinutes;
                
                if (PagePlaySoundWithBreakReminderCheckBox != null)
                    PagePlaySoundWithBreakReminderCheckBox.IsChecked = currentSettings.PlaySoundWithBreakReminder;

                // Populate sound settings
                PopulatePageNotificationSoundComboBox();
                
                // Load volume setting
                if (PageNotificationVolumeSlider != null && PageVolumeValueText != null)
                {
                    var volume = currentSettings.NotificationVolume;
                    PageNotificationVolumeSlider.Value = volume;
                    PageVolumeValueText.Text = $"{volume}%";
                }

                // Clear loading flag after settings are loaded
                _isLoadingPageSettings = false;

                // Show preferences content and hide main content
                preferencesContent.Visibility = Visibility.Visible;
                screenTimeContent.Visibility = Visibility.Collapsed;

                // Trigger Windows-style floating animations for settings cards
                TriggerSettingsCardAnimations();

                System.Diagnostics.Debug.WriteLine("MainWindow: Preferences page shown with current settings loaded and animations triggered");
            }
            catch (Exception ex)
            {
                _isLoadingPageSettings = false; // Ensure flag is cleared on error
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing preferences page: {ex.Message}");
            }
        }

        private void TriggerSettingsCardAnimations()
        {
            try
            {
                // Find the settings cards and trigger staggered animations
                var systemCard = this.FindName("SystemSettingsCard") as Wpf.Ui.Controls.Card;
                var notificationsCard = this.FindName("NotificationsSettingsCard") as Wpf.Ui.Controls.Card;

                if (systemCard != null)
                {
                    var storyboard1 = this.FindResource("SettingsCard1FloatInAnimation") as Storyboard;
                    if (storyboard1 != null)
                    {
                        Storyboard.SetTarget(storyboard1, systemCard);
                        storyboard1.Begin();
                    }
                }

                if (notificationsCard != null)
                {
                    var storyboard2 = this.FindResource("SettingsCard2FloatInAnimation") as Storyboard;
                    if (storyboard2 != null)
                    {
                        Storyboard.SetTarget(storyboard2, notificationsCard);
                        storyboard2.Begin();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error triggering settings card animations: {ex.Message}");
            }
        }

        private void HidePreferencesPage()
        {
            try
            {
                var preferencesContent = PreferencesContent;
                var screenTimeContent = ScreenTimeContent;

                if (preferencesContent == null || screenTimeContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - PreferencesContent or ScreenTimeContent is null");
                    return;
                }

                // Reset settings cards to initial state for next time
                ResetSettingsCardsForNextShow();

                // Animate the transition back to main content
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, preferencesContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        preferencesContent.Visibility = Visibility.Collapsed;
                        screenTimeContent.Visibility = Visibility.Visible;
                        
                        // Trigger fade-in animation for main content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, screenTimeContent);
                            fadeInStoryboard.Begin();
                        }
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    preferencesContent.Visibility = Visibility.Collapsed;
                    screenTimeContent.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error hiding preferences page: {ex.Message}");
            }
        }

        private void ResetSettingsCardsForNextShow()
        {
            try
            {
                var systemCard = this.FindName("SystemSettingsCard") as Wpf.Ui.Controls.Card;
                var notificationsCard = this.FindName("NotificationsSettingsCard") as Wpf.Ui.Controls.Card;

                // Reset system card
                if (systemCard?.RenderTransform is TransformGroup systemTransform)
                {
                    var systemTranslate = systemTransform.Children.OfType<TranslateTransform>().FirstOrDefault();
                    var systemScale = systemTransform.Children.OfType<ScaleTransform>().FirstOrDefault();
                    
                    if (systemTranslate != null)
                    {
                        systemTranslate.Y = 40;
                    }
                    if (systemScale != null)
                    {
                        systemScale.ScaleX = 0.95;
                        systemScale.ScaleY = 0.95;
                    }
                    systemCard.Opacity = 0;
                }

                // Reset notifications card
                if (notificationsCard?.RenderTransform is TransformGroup notificationsTransform)
                {
                    var notificationsTranslate = notificationsTransform.Children.OfType<TranslateTransform>().FirstOrDefault();
                    var notificationsScale = notificationsTransform.Children.OfType<ScaleTransform>().FirstOrDefault();
                    
                    if (notificationsTranslate != null)
                    {
                        notificationsTranslate.Y = 40;
                    }
                    if (notificationsScale != null)
                    {
                        notificationsScale.ScaleX = 0.95;
                        notificationsScale.ScaleY = 0.95;
                    }
                    notificationsCard.Opacity = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting settings cards: {ex.Message}");
            }
        }

        private void PageThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                string? theme = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(theme))
                {
                    ApplySavedTheme(theme);
                }
            }
        }

        private async void ShowLiveDashboard_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Live dashboard feature is coming soon!");
        }

        private async void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Export to CSV feature is coming soon!");
        }

        private async void ExportCharts_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Export charts feature is coming soon!");
        }

        private async void OpenDataFile_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Open data file feature is coming soon!");
        }

        private async void AutoExportSettings_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Auto export settings feature is coming soon!");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private async void ShowPieChart_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Pie chart feature is coming soon!");
        }

        private async void ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Bar chart feature is coming soon!");
        }

        private async void ManageCategories_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Category management feature is coming soon!");
        }

        private async void ViewByCategory_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "View by category feature is coming soon!");
        }

        private async void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.Topmost = !this.Topmost;
                        _settingsService.UpdateSettings(s => s.AlwaysOnTop = this.Topmost);
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error toggling always on top: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to toggle always on top: {ex.Message}");
            }
        }

        private void ShowInTray_Click(object sender, RoutedEventArgs e)
        {
            _isMinimizeToTrayEnabled = !_isMinimizeToTrayEnabled;
            _settingsService.UpdateSettings(s => s.ShowInSystemTray = _isMinimizeToTrayEnabled);
            
            if (_taskbarIcon != null)
            {
                _taskbarIcon.Visibility = _isMinimizeToTrayEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void HideTitleBar_Click(object sender, RoutedEventArgs e)
        {
            this.ExtendsContentIntoTitleBar = !this.ExtendsContentIntoTitleBar;
            _settingsService.UpdateSettings(s => s.HideTitleBar = this.ExtendsContentIntoTitleBar);
        }

        private async void TrackIdleTime_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Idle time tracking feature is coming soon!");
        }

        private async void TrackSubProcesses_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Sub-process tracking feature is coming soon!");
        }

        private async void ProcessTreeAnalysis_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Process tree analysis feature is coming soon!");
        }

        private async void MostUsedApps_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Most used apps feature is coming soon!");
        }

        private async void BreakNotifications_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Break notifications feature is coming soon!");
        }

        private async void ScreenBreakNotifications_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowContentDialogAsync(
                    "Screen Break Notifications",
                    "This feature will be available in a future update.",
                    "OK"
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error in screen break notifications: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to show screen break notifications dialog: {ex.Message}");
            }
        }

        private async void AutoLogoutSettings_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Auto logout settings feature is coming soon!");
        }

        private async void DistractionBlocking_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Distraction blocking feature is coming soon!");
        }

        private async void SetGoals_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Goal setting feature is coming soon!");
        }

        private async void MergeEntries_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Merge entries feature is coming soon!");
        }

        private async void BackupSync_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Backup and sync feature is coming soon!");
        }

        private async void CleanOldData_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Clean old data feature is coming soon!");
        }

        private async void ShowTutorial_Click(object sender, RoutedEventArgs e)
        {
            await ShowTutorial();
        }

        private async void ShowShortcuts_Click(object sender, RoutedEventArgs e)
        {
            await ShowShortcuts();
        }

        private async void OpenSource_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/ghassanelgendy/chronos-screentime",
                        UseShellExecute = true
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error opening source URL: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to open source URL: {ex.Message}");
            }
        }

        private async void ShowAbout_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("About Chronos", 
                "Chronos Screen Time Tracker\nVersion 1.0.0\n\n" +
                "A modern, privacy-focused screen time tracking application.\n\n" +
                "Made with ❤️ by Ghassan Elgendy");
        }

        private void TestNotification_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get current settings from UI controls
                string? soundFileName = null;
                int volume = 50;

                // Get selected sound from combo box
                if (PageNotificationSoundComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
                {
                    soundFileName = selectedItem.Tag?.ToString();
                }

                // Get volume from slider
                if (PageNotificationVolumeSlider != null)
                {
                    volume = (int)PageNotificationVolumeSlider.Value;
                }

                // Show balloon notification
                if (_taskbarIcon != null)
                {
                    _taskbarIcon.ShowBalloonTip("Chronos Test Notification", 
                                              "This is a test notification with your selected sound and volume.", 
                                              BalloonIcon.Info);
                }

                // Play the selected sound with the selected volume
                if (!string.IsNullOrEmpty(soundFileName))
                {
                    PlaySoundPreviewWithVolume(soundFileName, volume);
                }

                System.Diagnostics.Debug.WriteLine($"Test notification triggered with sound: {soundFileName} at {volume}% volume");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in test notification: {ex.Message}");
                MessageBox.Show($"Failed to show test notification: {ex.Message}", "Test Notification Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Add all other event handlers from XAML here...
        #endregion

        #region Date Navigation Methods

        private async void ShowToday_Click(object sender, RoutedEventArgs e)
        {
            try
        {
            _currentPeriod = "Today";
            TimeLabel.Text = "Today's Screen Time";
            SwitchesLabel.Text = "Today's Switches";

                await ShowMainContent();
            RefreshAppList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing today's content: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to show today's content: {ex.Message}");
            }
        }

        private async Task ShowMainContent()
        {
            try
            {
                await Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Preparing main content...");
                    // Add any CPU-intensive initialization here
                });

                await Dispatcher.InvokeAsync(() =>
                {
                    var preferencesContent = PreferencesContent;
                    var screenTimeContent = ScreenTimeContent;

                    if (preferencesContent != null && screenTimeContent != null)
                    {
                        preferencesContent.Visibility = Visibility.Collapsed;
                        screenTimeContent.Visibility = Visibility.Visible;
                    }
                });

                System.Diagnostics.Debug.WriteLine("MainWindow: Main content shown successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing main content: {ex.Message}");
                throw;
            }
        }

        private void ShowYesterday_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Yesterday";
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowThisWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "This Week";
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowLastWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Last Week";
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowThisMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "This Month";
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowCustomRange_Click(object sender, RoutedEventArgs e)
        {
            _currentPeriod = "Custom Range";
            RefreshAppList();
            UpdateNavigationStats();
        }

        #endregion

        #region Category Filter Methods

        private void FilterByCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.NavigationViewItem navItem)
            {
                string category = navItem.Tag?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(category))
                {
                    _currentPeriod = category;
                    RefreshAppList();
                }
            }
        }

        private async Task ShowAllCategories()
        {
            try
            {
                await Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Loading all categories...");
                    // Add category loading logic here
                });

                await Dispatcher.InvokeAsync(() =>
        {
            RefreshAppList();
                    System.Diagnostics.Debug.WriteLine("MainWindow: Categories refreshed");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing all categories: {ex.Message}");
                throw;
            }
        }

        private async void ShowWeeklyReport_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Feature Preview", "Weekly report feature coming soon!");
        }

        #endregion

        #region Settings Helper Methods

        private void SetPageCheckBoxValue(string? name, bool value)
        {
            if (string.IsNullOrEmpty(name))
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Warning - Null or empty checkbox name provided");
                return;
            }

            try
            {
                var checkbox = this.FindName(name) as Wpf.Ui.Controls.ToggleSwitch;
                if (checkbox != null)
                {
                    checkbox.IsChecked = value;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow: Warning - Checkbox {name} not found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error setting checkbox value: {ex.Message}");
            }
        }

        private void SetPageTextBoxValue(string? name, string? value)
        {
            if (string.IsNullOrEmpty(name))
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Warning - Null or empty textbox name provided");
                return;
            }

            try
            {
                var textbox = this.FindName(name) as Wpf.Ui.Controls.TextBox;
                if (textbox != null)
                {
                    textbox.Text = value ?? string.Empty;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow: Warning - TextBox {name} not found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error setting textbox value: {ex.Message}");
            }
        }

        private bool GetPageCheckBoxValue(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var control = FindName(name);
            if (control is Wpf.Ui.Controls.ToggleSwitch toggleSwitch)
            {
                return toggleSwitch.IsChecked == true;
            }
            return false;
        }

        private int GetPageIntTextBoxValue(string? name, int defaultValue)
        {
            if (string.IsNullOrEmpty(name))
                return defaultValue;

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

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // Refresh data whenever the window gains focus
            RefreshAppList();
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

        private async Task ShowTutorial()
        {
            await Task.Run(() =>
            {
                // Tutorial implementation will go here
                System.Diagnostics.Debug.WriteLine("Tutorial shown");
            });
        }

        private async Task ShowShortcuts()
        {
            await Task.Run(() =>
            {
                // Shortcuts implementation will go here
                System.Diagnostics.Debug.WriteLine("Shortcuts shown");
            });
        }

        private int GetIso8601WeekOfYear(DateTime date)
        {
            // Implementation of GetIso8601WeekOfYear method
            // This is a placeholder and should be replaced with the actual implementation
            return 0; // Placeholder return, actual implementation needed
        }
    }
}
