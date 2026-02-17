using chronos_screentime.Models;
using chronos_screentime.Services;
using chronos_screentime.Windows;
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
using System.Windows.Documents;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Collections.Generic;

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
        private readonly Services.ExportService _exportService;
        private readonly SleepService _sleepService;
        private readonly PowerSchedulingService _powerSchedulingService;
        private readonly ChartService _chartService;
        private readonly ChartRendererService _chartRendererService;
        private SupabaseUploadService? _supabaseUploadService;
        private System.Timers.Timer? _supabaseUploadTimer;
        private bool _isTracking = false;
        private DateTime _trackingStartTime;
        private string _currentPeriod = "Today";
        private bool _isLoadingPageSettings = false;
        private AppSettings? _workingPageSettings;
        private TaskbarIcon? _taskbarIcon;
        private bool _isMinimizeToTrayEnabled = false;
        private bool _isClosingToTray = false;
        private WindowState _previousWindowState = WindowState.Normal;
        private bool _isTimeRangeChange = false;
        private string? _currentCategoryFilter = null; // Add category filter state
        private AppScreenTime? _lastRightClickedApp;
        public ICommand MoveAppToCategoryCommand { get; }
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

            // Subscribe to settings changes
            _settingsService.SettingsChanged += OnSettingsChanged;
                System.Diagnostics.Debug.WriteLine("MainWindow: Settings change subscription added");

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

            // Apply initial idle threshold from settings
            _screenTimeService.UpdateIdleThreshold(_settingsService.CurrentSettings.IdleThresholdMinutes);



            // Initialize export service
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing export service...");
            _exportService = new Services.ExportService(_screenTimeService);
                System.Diagnostics.Debug.WriteLine("MainWindow: Export service initialized");

            // Initialize sleep service
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing sleep service...");
            _sleepService = new SleepService();
                System.Diagnostics.Debug.WriteLine("MainWindow: Sleep service initialized");

            // Initialize power scheduling service
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing power scheduling service...");
            _powerSchedulingService = new PowerSchedulingService();
                System.Diagnostics.Debug.WriteLine("MainWindow: Power scheduling service initialized");

            // Initialize chart services
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing chart services...");
            var categoryService = _screenTimeService.GetCategoryService();
            _chartService = new ChartService(_screenTimeService, categoryService);
            _chartRendererService = new ChartRendererService();
                System.Diagnostics.Debug.WriteLine("MainWindow: Chart services initialized");

            // Initialize Supabase upload service
                System.Diagnostics.Debug.WriteLine("MainWindow: Initializing Supabase upload service...");
            InitializeSupabaseUploadService();
                System.Diagnostics.Debug.WriteLine("MainWindow: Supabase upload service initialized");

            // Initialize custom category navigation
            RefreshCustomCategoryNavigation();

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

                // Timer for real-time app list updates
                var appListUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                appListUpdateTimer.Tick += (s, e) => RefreshAppList();
                appListUpdateTimer.Start();

                // Timer for real-time website list updates
                var websiteListUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                websiteListUpdateTimer.Tick += (s, e) => 
                {
                    if (WebBrowsingContent?.Visibility == Visibility.Visible)
                    {
                        RefreshWebsiteList();
                        UpdateWebBrowsingStats();
                    }
                };
                websiteListUpdateTimer.Start();

                // Timer for power scheduling status updates
                var powerScheduleUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                powerScheduleUpdateTimer.Tick += (s, e) => UpdatePowerScheduleStatus();
                powerScheduleUpdateTimer.Start();

                System.Diagnostics.Debug.WriteLine("MainWindow: UI update timers started");

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

            // Auto-categorize existing apps and websites after a short delay to ensure data is loaded
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting up delayed auto-categorization...");
            var autoCategorizeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2) // Wait 2 seconds after startup
            };
            autoCategorizeTimer.Tick += (s, e) =>
            {
                autoCategorizeTimer.Stop();
                try
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Starting delayed auto-categorization...");
                    var categoryService = _screenTimeService.GetCategoryService();
                    var allApps = _screenTimeService.GetAllApps().Select(a => a.AppName);
                    var allWebsiteDomains = _screenTimeService.GetAllWebsites().Select(w => w.Domain);
                    
                    // Auto-categorize if no categories exist
                    categoryService.AutoCategorizeIfNoCategoriesExist(allApps, allWebsiteDomains);
                    
                    // Also categorize any existing uncategorized items
                    categoryService.CategorizeExistingUncategorizedItems(allApps, allWebsiteDomains);
                    
                    // Refresh categories in the screen time service
                    _screenTimeService.RefreshAppCategories();
                    _screenTimeService.RefreshWebsiteCategories();
                    
                    // Refresh the UI to show the new categories
                    RefreshAppList();
                    
                    System.Diagnostics.Debug.WriteLine($"MainWindow: Delayed auto-categorization completed. Apps: {allApps.Count()}, Websites: {allWebsiteDomains.Count()}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow: Error during delayed auto-categorization: {ex.Message}");
                }
            };
            autoCategorizeTimer.Start();

            // Subscribe to window state change events
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting up window state handlers...");
            this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;
                System.Diagnostics.Debug.WriteLine("MainWindow: Window state handlers set");

            // Set the main window reference for update service
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting update service main window reference...");
            Services.UpdateService.SetMainWindow(this);
                System.Diagnostics.Debug.WriteLine("MainWindow: Update service reference set");

            // Check for updates after a short delay to allow UI to load
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting up update check...");
            var updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Wait 5 seconds after startup
            };
            updateTimer.Tick += async (s, e) =>
            {
                updateTimer.Stop();
                try
                {
                    await Services.UpdateService.CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
                }
            };
            updateTimer.Start();
                System.Diagnostics.Debug.WriteLine("MainWindow: Update check timer started");

                System.Diagnostics.Debug.WriteLine("MainWindow: Initialization completed successfully");
                }
                catch (Exception ex)
                {
                System.Diagnostics.Debug.WriteLine($"MainWindow: FATAL ERROR during initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error initializing main window: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
                }

            MoveAppToCategoryCommand = new RelayCommand<string>(MoveAppToCategory);
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
                            var executablePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
                            var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
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
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Setting responsive window size...");

                // Get the primary screen resolution
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                // Calculate window size based on screen resolution
                // Base ratio: 996x893 for 1920x1080
                double baseWidth = 996;
                double baseHeight = 893;
                double baseScreenWidth = 1920;
                double baseScreenHeight = 1080;

                // Calculate scale factor based on screen width (since width is usually the limiting factor)
                double scaleFactor = Math.Min(screenWidth / baseScreenWidth, screenHeight / baseScreenHeight);

                // Apply minimum and maximum constraints
                double targetWidth = Math.Max(800, Math.Min(screenWidth * 0.8, baseWidth * scaleFactor));
                double targetHeight = Math.Max(600, Math.Min(screenHeight * 0.8, baseHeight * scaleFactor));

                // Set window size
                this.Width = targetWidth;
                this.Height = targetHeight;

                // Center the window
                this.Left = (screenWidth - this.Width) / 2;
                this.Top = (screenHeight - this.Height) / 2;

                System.Diagnostics.Debug.WriteLine($"MainWindow: Window size set to {this.Width}x{this.Height} (Screen: {screenWidth}x{screenHeight})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error setting window size: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Stack trace: {ex.StackTrace}");
                
                // Fallback to default size if there's an error
                this.Width = 996;
                this.Height = 893;
            }
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
                    _ => Wpf.Ui.Appearance.ApplicationTheme.Light // Default to light theme
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
                    _ => Wpf.Ui.Appearance.ApplicationTheme.Light
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

        public async Task ShowInfoDialogAsync(string title, string message)
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

            // Unsubscribe from settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }

            _screenTimeService?.Dispose();
            _uiUpdateTimer?.Stop();
            _breakNotificationService?.Dispose();
            
            // Dispose Supabase upload service
            _supabaseUploadTimer?.Stop();
            _supabaseUploadTimer?.Dispose();
            _supabaseUploadService?.Dispose();

            // Dispose of tray icon resources
            if (_taskbarIcon != null)
            {
                _taskbarIcon.Dispose();
                _taskbarIcon = null;
            }

            base.OnClosed(e);
        }

        private void OnSettingsChanged(object? sender, AppSettings newSettings)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Settings changed, applying new settings...");
                ApplySettings(newSettings);

                // Update idle threshold in screen time service when settings change
                _screenTimeService.UpdateIdleThreshold(newSettings.IdleThresholdMinutes);
                
                // Reinitialize Supabase upload service if Supabase settings changed
                InitializeSupabaseUploadService();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error applying settings change: {ex.Message}");
            }
        }
        #endregion

        #region Settings Management
        private void ApplySettings(AppSettings settings)
        {
            try
            {
                // Apply general settings
                this.Topmost = settings.AlwaysOnTop;
                _isMinimizeToTrayEnabled = settings.ShowInSystemTray;

                // Apply title bar visibility
                if (MainTitleBar != null)
                {
                    MainTitleBar.Visibility = settings.HideTitleBar ? Visibility.Collapsed : Visibility.Visible;
                }

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
                if (StartWithWindowsMenuItem != null)
                    StartWithWindowsMenuItem.IsChecked = settings.StartWithWindows != "No";
                if (StartWithWindowsStatusText != null)
                    StartWithWindowsStatusText.Text = settings.StartWithWindows;

                // Apply navigation visibility settings
                ApplyNavigationVisibility(settings);

                System.Diagnostics.Debug.WriteLine($"Settings applied - AlwaysOnTop: {settings.AlwaysOnTop}, ShowInTray: {settings.ShowInSystemTray}, Theme: {settings.Theme}, HideTitleBar: {settings.HideTitleBar}, StartWithWindows: {settings.StartWithWindows}");
                UpdateCustomCategoryTogglesPanel(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying settings: {ex.Message}");
            }
        }

        private void ApplyNavigationVisibility(AppSettings settings)
        {
            try
            {
                // Apply visibility to time-based navigation items
                if (TodayNavItem != null)
                    TodayNavItem.Visibility = settings.ShowTodayTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (YesterdayNavItem != null)
                    YesterdayNavItem.Visibility = settings.ShowYesterdayTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (ThisWeekNavItem != null)
                    ThisWeekNavItem.Visibility = settings.ShowThisWeekTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (LastWeekNavItem != null)
                    LastWeekNavItem.Visibility = settings.ShowLastWeekTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (ThisMonthNavItem != null)
                    ThisMonthNavItem.Visibility = settings.ShowThisMonthTab ? Visibility.Visible : Visibility.Collapsed;

                // Apply visibility to special navigation items
                if (SleepNavItem != null)
                    SleepNavItem.Visibility = settings.ShowSleepTab ? Visibility.Visible : Visibility.Collapsed;

                // Apply visibility to category navigation items
                if (WebBrowsingNavItem != null)
                    WebBrowsingNavItem.Visibility = settings.ShowWebBrowsingTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (DevelopmentNavItem != null)
                    DevelopmentNavItem.Visibility = settings.ShowDevelopmentTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (GamingNavItem != null)
                    GamingNavItem.Visibility = settings.ShowGamingTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (CommunicationNavItem != null)
                    CommunicationNavItem.Visibility = settings.ShowCommunicationTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (ProductivityNavItem != null)
                    ProductivityNavItem.Visibility = settings.ShowProductivityTab ? Visibility.Visible : Visibility.Collapsed;
                
                if (EntertainmentNavItem != null)
                    EntertainmentNavItem.Visibility = settings.ShowEntertainmentTab ? Visibility.Visible : Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine("MainWindow: Navigation visibility applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error applying navigation visibility: {ex.Message}");
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

                if (StartWithWindowsMenuItem != null)
                    StartWithWindowsMenuItem.IsChecked = settings.StartWithWindows != "No";
                if (StartWithWindowsStatusText != null)
                    StartWithWindowsStatusText.Text = settings.StartWithWindows;

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

        private void ShowSaveConfirmation(string message = "? Changes saved")
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

        #region Supabase Upload Service
        private void InitializeSupabaseUploadService()
        {
            try
            {
                var settings = _settingsService.CurrentSettings;
                
                if (settings.EnableSupabaseSync && 
                    !string.IsNullOrWhiteSpace(settings.SupabaseUrl) && 
                    !string.IsNullOrWhiteSpace(settings.SupabaseAnonKey) &&
                    !string.IsNullOrWhiteSpace(settings.SupabaseUserId))
                {
                    // Dispose existing service if any
                    _supabaseUploadService?.Dispose();
                    _supabaseUploadTimer?.Dispose();

                    // Create new upload service
                    _supabaseUploadService = new SupabaseUploadService(
                        settings.SupabaseUrl,
                        settings.SupabaseAnonKey,
                        settings.SupabaseUserId,
                        Environment.MachineName
                    );

                    // Setup timer for automatic uploads (default 30 minutes)
                    var uploadIntervalMinutes = settings.SupabaseUploadIntervalMinutes > 0 
                        ? settings.SupabaseUploadIntervalMinutes 
                        : 30;
                    
                    _supabaseUploadTimer = new System.Timers.Timer(TimeSpan.FromMinutes(uploadIntervalMinutes).TotalMilliseconds);
                    _supabaseUploadTimer.Elapsed += async (sender, e) => await OnSupabaseUploadTimerElapsed();
                    _supabaseUploadTimer.AutoReset = true;
                    _supabaseUploadTimer.Start();

                    System.Diagnostics.Debug.WriteLine($"Supabase upload service initialized. Upload interval: {uploadIntervalMinutes} minutes");
                    
                    // Perform initial upload after a short delay (30 seconds) to allow app to fully initialize
                    var initialUploadTimer = new System.Timers.Timer(30000); // 30 seconds
                    initialUploadTimer.Elapsed += async (sender, e) =>
                    {
                        initialUploadTimer.Stop();
                        initialUploadTimer.Dispose();
                        await PerformSupabaseUpload();
                    };
                    initialUploadTimer.Start();
                }
                else
                {
                    // Disable upload service if settings are invalid
                    _supabaseUploadService?.Dispose();
                    _supabaseUploadService = null;
                    _supabaseUploadTimer?.Stop();
                    _supabaseUploadTimer?.Dispose();
                    _supabaseUploadTimer = null;
                    System.Diagnostics.Debug.WriteLine("Supabase upload service disabled - missing configuration");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing Supabase upload service: {ex.Message}");
            }
        }

        private async Task OnSupabaseUploadTimerElapsed()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Supabase upload timer elapsed - starting upload...");
                await PerformSupabaseUpload();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Supabase upload timer: {ex.Message}");
            }
        }

        private async Task PerformSupabaseUpload()
        {
            if (_supabaseUploadService == null || _screenTimeService == null)
            {
                return;
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                
                // Double-check settings are still valid
                if (!settings.EnableSupabaseSync || 
                    string.IsNullOrWhiteSpace(settings.SupabaseUserId))
                {
                    System.Diagnostics.Debug.WriteLine("Supabase upload skipped - sync disabled or user ID missing");
                    return;
                }

                // Ensure we upload what's on disk: save in-memory state then reload from screentime_data.json
                _screenTimeService.PrepareDataForUpload();
                
                // Get screen time data (now in sync with screentime_data.json)
                var screenTimeData = _screenTimeService.GetScreenTimeData();
                
                System.Diagnostics.Debug.WriteLine($"Got ScreenTimeData: {screenTimeData.Years.Count} years");
                foreach (var yearKvp in screenTimeData.Years)
                {
                    foreach (var monthKvp in yearKvp.Value.Months)
                    {
                        foreach (var weekKvp in monthKvp.Value.Weeks)
                        {
                            foreach (var dayKvp in weekKvp.Value.Days)
                            {
                                System.Diagnostics.Debug.WriteLine($"Day {dayKvp.Value.Date:yyyy-MM-dd}: {dayKvp.Value.Apps.Count} apps, TotalSwitches={dayKvp.Value.TotalSwitches}, TotalApps={dayKvp.Value.TotalApps}");
                            }
                        }
                    }
                }
                
                // Perform upload (pass interval so time-based gate uses same value as timer)
                var intervalMinutes = settings.SupabaseUploadIntervalMinutes > 0 ? settings.SupabaseUploadIntervalMinutes : 30;
                var result = await _supabaseUploadService.UploadScreentimeDataAsync(
                    screenTimeData,
                    settings.SupabaseUserId,
                    Environment.MachineName,
                    intervalMinutes
                );

                if (result.Success)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Supabase upload successful: {result.AppsInserted} apps, {result.WebsitesInserted} websites uploaded"
                    );
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Supabase upload failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error performing Supabase upload: {ex.Message}");
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
                        // Refresh main app list (preserves category filter)
                        RefreshAppList();

                        // Also refresh website data if Web Browsing page is visible
                        if (WebBrowsingContent?.Visibility == Visibility.Visible)
                        {
                            RefreshWebsiteList();
                            UpdateWebBrowsingStats();
                        }

                        // Refresh custom category navigation
                        RefreshCustomCategoryNavigation();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error refreshing app list: {ex.Message}");
                    }
                });
            }
        }

        private void RefreshAppList(List<AppScreenTime>? specificApps = null)
        {
            var today = DateTime.Today;
            var apps = specificApps ?? _screenTimeService.GetAllApps().ToList();
            List<AppScreenTime> filteredApps = new();

            // Apply category filter if active
            if (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "WebBrowsing")
            {
                apps = apps.Where(a => a.Category == _currentCategoryFilter).ToList();
            }

            switch (_currentPeriod)
            {
                case "Today":
                    filteredApps = apps.Where(a => a.DailyTimes.ContainsKey(today)).ToList();
                    break;

                case "Yesterday":
                    var yesterday = today.AddDays(-1);
                    filteredApps = apps.Where(a => a.DailyTimes.ContainsKey(yesterday)).ToList();
                    break;

                case "This Week":
                    var weekStart = today.AddDays(-(int)today.DayOfWeek);
                    filteredApps = apps.Where(a => a.DailyTimes.Any(dt => 
                        dt.Key >= weekStart && dt.Key <= today)).ToList();
                    break;

                case "Last Week":
                    var lastWeekStart = today.AddDays(-(int)today.DayOfWeek - 7);
                    var lastWeekEnd = lastWeekStart.AddDays(6);
                    filteredApps = apps.Where(a => a.DailyTimes.Any(dt => 
                        dt.Key >= lastWeekStart && dt.Key <= lastWeekEnd)).ToList();
                    break;

                case "This Month":
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    filteredApps = apps.Where(a => a.DailyTimes.Any(dt => 
                        dt.Key >= monthStart && dt.Key <= today)).ToList();
                    break;

                default:
                    filteredApps = apps;
                    break;
            }

            // Only enable animations if this is a time range change
            if (_isTimeRangeChange)
            {
                foreach (var card in AppListView.Items.OfType<FrameworkElement>())
                {
                    card.Opacity = 0;
                }
            }

            // If we have a category filter, include websites from that category
            if (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "WebBrowsing")
            {
                var websites = _screenTimeService.GetAllWebsites().ToList();
                var filteredWebsites = websites.Where(w => w.Category == _currentCategoryFilter).ToList();

                // Filter websites by current period
                var periodFilteredWebsites = _currentPeriod switch
                {
                    "Today" => filteredWebsites.Where(w => w.DailyTimes.ContainsKey(today)).ToList(),
                    "Yesterday" => filteredWebsites.Where(w => w.DailyTimes.ContainsKey(today.AddDays(-1))).ToList(),
                    "This Week" => filteredWebsites.Where(w => w.DailyTimes.Any(dt => 
                        dt.Key >= today.AddDays(-(int)today.DayOfWeek) && dt.Key <= today)).ToList(),
                    "Last Week" => filteredWebsites.Where(w => w.DailyTimes.Any(dt => 
                        dt.Key >= today.AddDays(-(int)today.DayOfWeek - 7) && 
                        dt.Key <= today.AddDays(-(int)today.DayOfWeek - 1))).ToList(),
                    "This Month" => filteredWebsites.Where(w => w.DailyTimes.Any(dt => 
                        dt.Key.Year == today.Year && dt.Key.Month == today.Month)).ToList(),
                    _ => filteredWebsites
                };

                // Create combined data with both apps and websites
                var appDisplayData = filteredApps.Select(a => new
                {
                    Name = a.AppName,
                    Type = "App",
                    ProcessPath = a.ProcessPath ?? string.Empty,
                    TotalTime = a.TotalTime,
                    FormattedTotalTimeShort = GetFormattedTimeShort(GetTimeForCurrentPeriod(a)),
                    SessionCount = _currentPeriod == "Today" ? a.TodaysSessionCount :
                                 _currentPeriod == "Yesterday" ? a.GetSessionsForDate(today.AddDays(-1)) :
                                 a.SessionCount,
                    LastSeen = a.LastSeen,
                    FaviconUrl = (string?)null
                });

                var websiteDisplayData = periodFilteredWebsites.Select(w => new
                {
                    Name = w.DisplayName,
                    Type = "Website",
                    ProcessPath = string.Empty,
                    TotalTime = w.TotalTime,
                    FormattedTotalTimeShort = GetFormattedTimeShort(GetTimeForCurrentPeriod(w)),
                    SessionCount = _currentPeriod == "Today" ? w.TodaysSessionCount :
                                 _currentPeriod == "Yesterday" ? w.GetSessionsForDate(today.AddDays(-1)) :
                                 w.SessionCount,
                    LastSeen = w.LastSeen,
                    FaviconUrl = w.FaviconUrl
                });

                // Combine and sort by time
                var combinedData = appDisplayData.Concat(websiteDisplayData)
                    .OrderByDescending(item => 
                    {
                        if (item.Type == "App")
                        {
                            var app = filteredApps.First(fa => fa.AppName == item.Name);
                            return GetTimeForCurrentPeriod(app).TotalMilliseconds;
                        }
                        else
                        {
                            var website = periodFilteredWebsites.First(fw => fw.DisplayName == item.Name);
                            return GetTimeForCurrentPeriod(website).TotalMilliseconds;
                        }
                    });

                AppListView.ItemsSource = combinedData;

                // Update summary with combined data
                var combinedApps = filteredApps.Concat(periodFilteredWebsites.Select(w => new AppScreenTime
                {
                    AppName = w.DisplayName,
                    Category = w.Category,
                    TotalTime = w.TotalTime,
                    DailyTimes = w.DailyTimes,
                    SessionCount = w.SessionCount,
                    LastSeen = w.LastSeen
                })).ToList();

                UpdateSummaryUI(combinedApps);
            }
            else
            {
                // Create UI-friendly app data with time based on current period (apps only)
                var appDisplayData = filteredApps.Select(a => new
                {
                    Name = a.AppName,
                    Type = "App",
                    ProcessPath = a.ProcessPath,
                    TotalTime = a.TotalTime,
                    FormattedTotalTimeShort = GetFormattedTimeShort(GetTimeForCurrentPeriod(a)),
                    SessionCount = _currentPeriod == "Today" ? a.TodaysSessionCount :
                                 _currentPeriod == "Yesterday" ? a.GetSessionsForDate(today.AddDays(-1)) :
                                 a.SessionCount,
                    LastSeen = a.LastSeen,
                    FaviconUrl = (string?)null
                }).OrderByDescending(a => GetTimeForCurrentPeriod(filteredApps.First(fa => fa.AppName == a.Name)).TotalMilliseconds);

                AppListView.ItemsSource = appDisplayData;
                UpdateSummaryUI(filteredApps);
            }

            // Reset the flag after updating
            _isTimeRangeChange = false;

            UpdateNavigationStats();
            UpdateCategoryFilterButtonVisibility();
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
                        UpdateCategoryFilterButtonVisibility();
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

        private void UpdatePowerScheduleStatus()
        {
            try
            {
                if (PowerScheduleStatusMenuItem != null)
                {
                    var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
                    
                    if (scheduleInfo.IsScheduled)
                    {
                        var actionText = scheduleInfo.Action == PowerSchedulingService.PowerAction.Shutdown ? "Power Off" : "Restart";
                        var timeText = FormatTimeSpan(scheduleInfo.TimeRemaining);
                        
                        if (scheduleInfo.IsPaused)
                        {
                            PowerScheduleStatusMenuItem.Header = $"⏸️ {actionText} (PAUSED) - {timeText} remaining";
                        }
                        else
                        {
                            PowerScheduleStatusMenuItem.Header = $"⏰ {actionText} in {timeText}";
                        }
                    }
                    else
                    {
                        PowerScheduleStatusMenuItem.Header = "No schedule active";
                    }
                }

                // Update cancel schedule menu item
                if (PowerCancelScheduleMenuItem != null)
                {
                    var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
                    PowerCancelScheduleMenuItem.IsEnabled = scheduleInfo.IsScheduled;
                }

                // Also update preferences page if it's visible
                if (PreferencesContent?.Visibility == Visibility.Visible)
                {
                    UpdatePowerScheduleStatusInPreferences();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating power schedule status: {ex.Message}");
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else if (timeSpan.Minutes >= 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                return $"{timeSpan.Seconds}s";
            }
        }

        private void UpdatePowerScheduleStatusInPreferences()
        {
            try
            {
                // Update the button text to show current status
                if (OpenPowerSchedulingDialogButton != null)
                {
                    var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
                    
                    if (scheduleInfo.IsScheduled)
                    {
                        var actionText = scheduleInfo.Action == PowerSchedulingService.PowerAction.Shutdown ? "Power Off" : "Restart";
                        var timeText = FormatTimeSpan(scheduleInfo.TimeRemaining);
                        
                        if (scheduleInfo.IsPaused)
                        {
                            OpenPowerSchedulingDialogButton.Content = $"⏸️ {actionText} (PAUSED) - {timeText} remaining";
                        }
                        else
                        {
                            OpenPowerSchedulingDialogButton.Content = $"⏰ {actionText} in {timeText}";
                        }
                    }
                    else
                    {
                        OpenPowerSchedulingDialogButton.Content = "⚡ Open Power Scheduling Dialog";
                    }
                }

                // Update the enable checkbox to reflect current state
                if (PageEnablePowerSchedulingCheckBox != null)
                {
                    var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
                    var hasActiveSchedule = scheduleInfo.IsScheduled;
                    var isEnabledInSettings = _settingsService?.CurrentSettings?.EnablePowerScheduling ?? false;
                    
                    // Show the checkbox as enabled if there's an active schedule OR if it's enabled in settings
                    // But don't disable the checkbox - let the user control it
                    if (hasActiveSchedule || isEnabledInSettings)
                    {
                        PageEnablePowerSchedulingCheckBox.IsChecked = true;
                    }
                    else
                    {
                        // If no active schedule and not enabled in settings, show current settings state
                        // but allow user to change it
                        PageEnablePowerSchedulingCheckBox.IsChecked = isEnabledInSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating power schedule status in preferences: {ex.Message}");
            }
        }

        private void UpdateNavigationStats()
        {
            if (SidebarCurrentPeriod != null)
            {
                // Update period text to include category if filtering
                if (!string.IsNullOrEmpty(_currentCategoryFilter))
                {
                    SidebarCurrentPeriod.Text = $"{_currentPeriod} - {_currentCategoryFilter}";
                }
                else
                {
                    SidebarCurrentPeriod.Text = _currentPeriod;
                }
            }

            var apps = _screenTimeService.GetAllApps().ToList();
            var today = DateTime.Today;

            // Filter apps by category if a category filter is active
            if (!string.IsNullOrEmpty(_currentCategoryFilter))
            {
                apps = apps.Where(a => a.Category == _currentCategoryFilter).ToList();
            }

            if (SidebarTotalTime != null)
            {
                TimeSpan totalTime = _currentPeriod switch
                {
                    "Today" => TimeSpan.FromMilliseconds(apps.Sum(a => a.TodaysTime.TotalMilliseconds)),
                    "Yesterday" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetTimeForDate(today.AddDays(-1)).TotalMilliseconds)),
                    "This Week" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek)).TotalMilliseconds)),
                    "Last Week" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek - 7)).TotalMilliseconds)),
                    "This Month" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetMonthTotal(today.Year, today.Month).TotalMilliseconds)),
                    _ => TimeSpan.FromMilliseconds(apps.Sum(a => a.TotalTime.TotalMilliseconds))
                };

                // If we have a category filter, also include websites
                if (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "WebBrowsing")
                {
                    var websites = _screenTimeService.GetAllWebsites().ToList();
                    var filteredWebsites = websites.Where(w => w.Category == _currentCategoryFilter).ToList();

                    TimeSpan websiteTime = _currentPeriod switch
                    {
                        "Today" => TimeSpan.FromMilliseconds(filteredWebsites.Sum(w => w.TodaysTime.TotalMilliseconds)),
                        "Yesterday" => TimeSpan.FromMilliseconds(filteredWebsites.Sum(w => w.GetTimeForDate(today.AddDays(-1)).TotalMilliseconds)),
                        "This Week" => TimeSpan.FromMilliseconds(filteredWebsites.Sum(w => w.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek)).TotalMilliseconds)),
                        "Last Week" => TimeSpan.FromMilliseconds(filteredWebsites.Sum(w => w.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek - 7)).TotalMilliseconds)),
                        "This Month" => TimeSpan.FromMilliseconds(filteredWebsites.Sum(w => w.GetMonthTotal(today.Year, today.Month).TotalMilliseconds)),
                        _ => TimeSpan.FromMilliseconds(filteredWebsites.Sum(w => w.TotalTime.TotalMilliseconds))
                    };

                    totalTime = totalTime.Add(websiteTime);
                }

                var hours = (int)totalTime.TotalHours;
                var minutes = totalTime.Minutes;
                SidebarTotalTime.Text = $"{hours}h {minutes}m";

                int totalSwitches = _currentPeriod switch
                {
                    "Today" => apps.Sum(a => a.TodaysSessionCount),
                    "Yesterday" => apps.Sum(a => a.GetSessionsForDate(today.AddDays(-1))),
                    "This Week" => apps.Sum(a => Enumerable.Range(0, 7)
                        .Sum(i => a.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek + i)))),
                    "Last Week" => apps.Sum(a => Enumerable.Range(0, 7)
                        .Sum(i => a.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek - 7 + i)))),
                    "This Month" => apps.Sum(a => Enumerable.Range(0, DateTime.DaysInMonth(today.Year, today.Month))
                        .Sum(i => a.GetSessionsForDate(new DateTime(today.Year, today.Month, i + 1)))),
                    _ => apps.Sum(a => a.SessionCount)
                };

                // If we have a category filter, also include website switches
                if (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "WebBrowsing")
                {
                    var websites = _screenTimeService.GetAllWebsites().ToList();
                    var filteredWebsites = websites.Where(w => w.Category == _currentCategoryFilter).ToList();

                    int websiteSwitches = _currentPeriod switch
                    {
                        "Today" => filteredWebsites.Sum(w => w.TodaysSessionCount),
                        "Yesterday" => filteredWebsites.Sum(w => w.GetSessionsForDate(today.AddDays(-1))),
                        "This Week" => filteredWebsites.Sum(w => Enumerable.Range(0, 7)
                            .Sum(i => w.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek + i)))),
                        "Last Week" => filteredWebsites.Sum(w => Enumerable.Range(0, 7)
                            .Sum(i => w.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek - 7 + i)))),
                        "This Month" => filteredWebsites.Sum(w => Enumerable.Range(0, DateTime.DaysInMonth(today.Year, today.Month))
                            .Sum(i => w.GetSessionsForDate(new DateTime(today.Year, today.Month, i + 1)))),
                        _ => filteredWebsites.Sum(w => w.SessionCount)
                    };

                    totalSwitches += websiteSwitches;
                }

                SidebarSwitches.Text = $"{totalSwitches} switches";

                int totalApps = _currentPeriod switch
                {
                    "Today" => apps.Count(a => a.DailyTimes.ContainsKey(today)),
                    "Yesterday" => apps.Count(a => a.DailyTimes.ContainsKey(today.AddDays(-1))),
                    "This Week" => apps.Count(a => a.DailyTimes.Any(dt => 
                        dt.Key >= today.AddDays(-(int)today.DayOfWeek) && dt.Key <= today)),
                    "Last Week" => apps.Count(a => a.DailyTimes.Any(dt => 
                        dt.Key >= today.AddDays(-(int)today.DayOfWeek - 7) && 
                        dt.Key <= today.AddDays(-(int)today.DayOfWeek - 1))),
                    "This Month" => apps.Count(a => a.DailyTimes.Any(dt => 
                        dt.Key.Year == today.Year && dt.Key.Month == today.Month)),
                    _ => apps.Count
                };

                // If we have a category filter, also include websites
                if (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "WebBrowsing")
                {
                    var websites = _screenTimeService.GetAllWebsites().ToList();
                    var filteredWebsites = websites.Where(w => w.Category == _currentCategoryFilter).ToList();

                    int totalWebsites = _currentPeriod switch
                    {
                        "Today" => filteredWebsites.Count(w => w.DailyTimes.ContainsKey(today)),
                        "Yesterday" => filteredWebsites.Count(w => w.DailyTimes.ContainsKey(today.AddDays(-1))),
                        "This Week" => filteredWebsites.Count(w => w.DailyTimes.Any(dt => 
                            dt.Key >= today.AddDays(-(int)today.DayOfWeek) && dt.Key <= today)),
                        "Last Week" => filteredWebsites.Count(w => w.DailyTimes.Any(dt => 
                            dt.Key >= today.AddDays(-(int)today.DayOfWeek - 7) && 
                            dt.Key <= today.AddDays(-(int)today.DayOfWeek - 1))),
                        "This Month" => filteredWebsites.Count(w => w.DailyTimes.Any(dt => 
                            dt.Key.Year == today.Year && dt.Key.Month == today.Month)),
                        _ => filteredWebsites.Count
                    };

                    totalApps += totalWebsites;
                }

                SidebarApps.Text = $"{totalApps} items";
            }
        }

        private void UpdateLabelsForCurrentContext()
        {
            // Update labels based on current period and category filter
            if (!string.IsNullOrEmpty(_currentCategoryFilter))
            {
                // Category-specific labels
                TimeLabel.Text = $"{_currentPeriod}'s {_currentCategoryFilter} Screen Time";
                SwitchesLabel.Text = $"{_currentPeriod}'s {_currentCategoryFilter} Switches";
            }
            else
            {
                // General labels
                TimeLabel.Text = $"{_currentPeriod}'s Screen Time";
                SwitchesLabel.Text = $"{_currentPeriod}'s Switches";
            }
        }

        private void UpdateSummaryUI(List<AppScreenTime> apps)
        {
            var today = DateTime.Today;
            
            // Update app count text to reflect category if filtering
            if (!string.IsNullOrEmpty(_currentCategoryFilter))
            {
                TotalAppsText.Text = $"{apps.Count} Items";
            }
            else
            {
                TotalAppsText.Text = apps.Count.ToString();
            }

            TimeSpan totalTime = _currentPeriod switch
            {
                "Today" => TimeSpan.FromMilliseconds(apps.Sum(a => a.TodaysTime.TotalMilliseconds)),
                "Yesterday" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetTimeForDate(today.AddDays(-1)).TotalMilliseconds)),
                "This Week" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek)).TotalMilliseconds)),
                "Last Week" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek - 7)).TotalMilliseconds)),
                "This Month" => TimeSpan.FromMilliseconds(apps.Sum(a => a.GetMonthTotal(today.Year, today.Month).TotalMilliseconds)),
                _ => TimeSpan.FromMilliseconds(apps.Sum(a => a.TotalTime.TotalMilliseconds))
            };

            var hours = (int)totalTime.TotalHours;
            var minutes = totalTime.Minutes;
            var seconds = totalTime.Seconds;
            TotalTimeText.Text = $"{hours}h {minutes}m {seconds}s";

            int totalSwitches = _currentPeriod switch
            {
                "Today" => apps.Sum(a => a.TodaysSessionCount),
                "Yesterday" => apps.Sum(a => a.GetSessionsForDate(today.AddDays(-1))),
                "This Week" => apps.Sum(a => Enumerable.Range(0, 7)
                    .Sum(i => a.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek + i)))),
                "Last Week" => apps.Sum(a => Enumerable.Range(0, 7)
                    .Sum(i => a.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek - 7 + i)))),
                "This Month" => apps.Sum(a => Enumerable.Range(0, DateTime.DaysInMonth(today.Year, today.Month))
                    .Sum(i => a.GetSessionsForDate(new DateTime(today.Year, today.Month, i + 1)))),
                _ => apps.Sum(a => a.SessionCount)
            };

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
            // Check which page is currently visible and navigate back to main
            if (WebBrowsingContent?.Visibility == Visibility.Visible)
            {
                HideWebBrowsingPage();
            }
            else if (PreferencesContent?.Visibility == Visibility.Visible)
            {
                HidePreferencesPage();
            }
            else if (SleepContent?.Visibility == Visibility.Visible)
            {
                HideSleepPage();
            }
            else if (ChartsContent?.Visibility == Visibility.Visible)
            {
                HideChartsPage();
            }
            else
            {
                // If we're on the main screen but have a category filter, clear it
                ClearCategoryFilter();
            }
        }

        private void ClearCategoryFilter()
        {
            if (!string.IsNullOrEmpty(_currentCategoryFilter))
            {
                _currentCategoryFilter = null;
                UpdateLabelsForCurrentContext();
                RefreshAppList();
                UpdateNavigationStats();
                UpdateCategoryFilterButtonVisibility();
            }
        }

        private void ClearCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            ClearCategoryFilter();
        }

        private void UpdateCategoryFilterButtonVisibility()
        {
            if (ClearCategoryFilterButton != null)
            {
                ClearCategoryFilterButton.Visibility = !string.IsNullOrEmpty(_currentCategoryFilter) 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void RefreshCustomCategoryNavigation()
        {
            try
            {
                var categoryService = _screenTimeService.GetCategoryService();
                var customCategories = categoryService.GetCustomCategories();
                var settings = _settingsService.CurrentSettings;
                var existingCustomItems = MainNavigationView.MenuItems.OfType<Wpf.Ui.Controls.NavigationViewItem>()
                    .Where(item => item.Tag?.ToString()?.StartsWith("Custom_") == true)
                    .ToList();
                foreach (var item in existingCustomItems)
                {
                    MainNavigationView.MenuItems.Remove(item);
                }
                // Check if custom categories should be shown in navigation at all
                if (!settings.ShowCustomCategoriesInCharts)
                {
                    return; // Don't show any custom categories
                }

                foreach (var category in customCategories)
                {
                    var show = settings.ShowCustomCategoryTabs.TryGetValue(category, out var enabled) ? enabled : true;
                    if (!show) continue;
                    var navItem = new Wpf.Ui.Controls.NavigationViewItem
                    {
                        Content = category,
                        Tag = $"Custom_{category}"
                    };
                    navItem.Click += FilterByCategory_Click;
                    var icon = GetCategoryIcon(category);
                    navItem.Icon = new Wpf.Ui.Controls.FontIcon { Glyph = icon };
                    MainNavigationView.MenuItems.Add(navItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing custom category navigation: {ex.Message}");
            }
        }

        private string GetCategoryIcon(string categoryName)
        {
            // Return appropriate icon based on category name
            return categoryName.ToLower() switch
            {
                var name when name.Contains("work") || name.Contains("business") => "💼",
                var name when name.Contains("study") || name.Contains("learn") => "📚",
                var name when name.Contains("game") || name.Contains("play") => "🎮",
                var name when name.Contains("social") || name.Contains("chat") => "💬",
                var name when name.Contains("video") || name.Contains("movie") => "🎬",
                var name when name.Contains("music") || name.Contains("audio") => "🎵",
                var name when name.Contains("photo") || name.Contains("image") => "📷",
                var name when name.Contains("shop") || name.Contains("buy") => "🛒",
                var name when name.Contains("news") || name.Contains("read") => "📰",
                var name when name.Contains("health") || name.Contains("fitness") => "💪",
                var name when name.Contains("finance") || name.Contains("money") => "💰",
                var name when name.Contains("travel") || name.Contains("map") => "✈️",
                _ => "📁" // Default icon
            };
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

                // Startup setting
                if (PageStartWithWindowsComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem startupItem)
                {
                    newSettings.StartWithWindows = startupItem.Tag?.ToString() ?? "No";
                }

                // Theme setting
                if (PageThemeComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem themeItem)
                {
                    var themeTag = themeItem.Tag?.ToString();
                    newSettings.Theme = themeTag switch
                    {
                        "Dark" => "Dark Theme",
                        _ => "Light Theme"
                    };
                }

                // Break notification settings
                if (PageEnableBreakNotificationsCheckBox != null)
                    newSettings.EnableBreakNotifications = PageEnableBreakNotificationsCheckBox.IsChecked ?? false;
                
                if (PageBreakReminderMinutesTextBox != null)
                    newSettings.BreakReminderMinutes = (int)(PageBreakReminderMinutesTextBox.Value > 0 ? PageBreakReminderMinutesTextBox.Value : 30);

                // Supabase sync settings
                if (PageEnableSupabaseSyncCheckBox != null)
                    newSettings.EnableSupabaseSync = PageEnableSupabaseSyncCheckBox.IsChecked ?? false;
                
                if (PageSupabaseUrlTextBox != null)
                    newSettings.SupabaseUrl = PageSupabaseUrlTextBox.Text?.Trim() ?? string.Empty;
                
                // Get the key from either password box (if visible) or text box (if showing)
                if (PageSupabaseAnonKeyPasswordBox != null && PageSupabaseAnonKeyPasswordBox.Visibility == Visibility.Visible)
                    newSettings.SupabaseAnonKey = PageSupabaseAnonKeyPasswordBox.Password ?? string.Empty;
                else if (PageSupabaseAnonKeyTextBox != null && PageSupabaseAnonKeyTextBox.Visibility == Visibility.Visible)
                    newSettings.SupabaseAnonKey = PageSupabaseAnonKeyTextBox.Text?.Trim() ?? string.Empty;
                
                if (PageSupabaseUserIdTextBox != null)
                    newSettings.SupabaseUserId = PageSupabaseUserIdTextBox.Text?.Trim() ?? string.Empty;
                
                if (PageSupabaseUploadIntervalMinutesTextBox != null)
                    newSettings.SupabaseUploadIntervalMinutes = (int)(PageSupabaseUploadIntervalMinutesTextBox.Value > 0 
                        ? PageSupabaseUploadIntervalMinutesTextBox.Value 
                        : 30);

                // Idle timeout settings
                if (PageIdleThresholdMinutesTextBox != null)
                    newSettings.IdleThresholdMinutes = (int)(PageIdleThresholdMinutesTextBox.Value >= 0
                        ? PageIdleThresholdMinutesTextBox.Value
                        : 5);

                // Sound settings
                if (PageNotificationSoundComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem soundItem)
                {
                    newSettings.NotificationSoundFile = soundItem.Tag?.ToString() ?? "Beep.wav";
                }
                
                if (PageNotificationVolumeSlider != null)
                    newSettings.NotificationVolume = (int)PageNotificationVolumeSlider.Value;

                // Navigation visibility settings
                if (PageShowTodayTabCheckBox != null)
                    newSettings.ShowTodayTab = PageShowTodayTabCheckBox.IsChecked ?? true;
                
                if (PageShowYesterdayTabCheckBox != null)
                    newSettings.ShowYesterdayTab = PageShowYesterdayTabCheckBox.IsChecked ?? true;
                
                if (PageShowThisWeekTabCheckBox != null)
                    newSettings.ShowThisWeekTab = PageShowThisWeekTabCheckBox.IsChecked ?? true;
                
                if (PageShowLastWeekTabCheckBox != null)
                    newSettings.ShowLastWeekTab = PageShowLastWeekTabCheckBox.IsChecked ?? true;
                
                if (PageShowThisMonthTabCheckBox != null)
                    newSettings.ShowThisMonthTab = PageShowThisMonthTabCheckBox.IsChecked ?? true;
                
                if (PageShowSleepTabCheckBox != null)
                    newSettings.ShowSleepTab = PageShowSleepTabCheckBox.IsChecked ?? true;
                
                if (PageShowWebBrowsingTabCheckBox != null)
                    newSettings.ShowWebBrowsingTab = PageShowWebBrowsingTabCheckBox.IsChecked ?? true;
                
                if (PageShowDevelopmentTabCheckBox != null)
                    newSettings.ShowDevelopmentTab = PageShowDevelopmentTabCheckBox.IsChecked ?? true;
                
                if (PageShowGamingTabCheckBox != null)
                    newSettings.ShowGamingTab = PageShowGamingTabCheckBox.IsChecked ?? true;
                
                if (PageShowCommunicationTabCheckBox != null)
                    newSettings.ShowCommunicationTab = PageShowCommunicationTabCheckBox.IsChecked ?? true;
                
                if (PageShowProductivityTabCheckBox != null)
                    newSettings.ShowProductivityTab = PageShowProductivityTabCheckBox.IsChecked ?? true;
                
                if (PageShowEntertainmentTabCheckBox != null)
                    newSettings.ShowEntertainmentTab = PageShowEntertainmentTabCheckBox.IsChecked ?? true;

                // Save custom category toggles first
                if (CustomCategoryTogglesPanel != null)
                {
                    var customTabs = new Dictionary<string, bool>();
                    foreach (var item in CustomCategoryTogglesPanel.Items)
                    {
                        if (item is Wpf.Ui.Controls.ToggleSwitch toggle && toggle.Tag is string category)
                        {
                            customTabs[category] = toggle.IsChecked ?? true;
                        }
                    }
                    newSettings.ShowCustomCategoryTabs = customTabs;
                }

                // Apply settings to the settings service (this writes to JSON)
                _settingsService?.UpdateSettings(s =>
                {
                    s.AlwaysOnTop = newSettings.AlwaysOnTop;
                    s.ShowInSystemTray = newSettings.ShowInSystemTray;
                    s.HideTitleBar = newSettings.HideTitleBar;
                    s.StartWithWindows = newSettings.StartWithWindows;
                    s.Theme = newSettings.Theme;
                    s.EnableBreakNotifications = newSettings.EnableBreakNotifications;
                    s.BreakReminderMinutes = newSettings.BreakReminderMinutes;

                    s.NotificationSoundFile = newSettings.NotificationSoundFile;
                    s.NotificationVolume = newSettings.NotificationVolume;
                    
                    // Navigation visibility settings
                    s.ShowTodayTab = newSettings.ShowTodayTab;
                    s.ShowYesterdayTab = newSettings.ShowYesterdayTab;
                    s.ShowThisWeekTab = newSettings.ShowThisWeekTab;
                    s.ShowLastWeekTab = newSettings.ShowLastWeekTab;
                    s.ShowThisMonthTab = newSettings.ShowThisMonthTab;
                    s.ShowSleepTab = newSettings.ShowSleepTab;
                    s.ShowWebBrowsingTab = newSettings.ShowWebBrowsingTab;
                    s.ShowDevelopmentTab = newSettings.ShowDevelopmentTab;
                    s.ShowGamingTab = newSettings.ShowGamingTab;
                    s.ShowCommunicationTab = newSettings.ShowCommunicationTab;
                    s.ShowProductivityTab = newSettings.ShowProductivityTab;
                    s.ShowEntertainmentTab = newSettings.ShowEntertainmentTab;
                    
                    // Custom category tab visibility settings
                    s.ShowCustomCategoryTabs = newSettings.ShowCustomCategoryTabs;
                    
                    // Chart filtering settings
                    s.ShowUncategorizedInCharts = newSettings.ShowUncategorizedInCharts;
                    s.ShowDevelopmentInCharts = newSettings.ShowDevelopmentInCharts;
                    s.ShowGamingInCharts = newSettings.ShowGamingInCharts;
                    s.ShowCommunicationInCharts = newSettings.ShowCommunicationInCharts;
                    s.ShowProductivityInCharts = newSettings.ShowProductivityInCharts;
                    s.ShowEntertainmentInCharts = newSettings.ShowEntertainmentInCharts;
                    s.ShowCustomCategoriesInCharts = newSettings.ShowCustomCategoriesInCharts;
                    s.EnableChartAnimations = newSettings.EnableChartAnimations;
                    
                    // Power scheduling settings
                    s.EnablePowerScheduling = newSettings.EnablePowerScheduling;
                    s.PowerScheduleHours = newSettings.PowerScheduleHours;
                    s.PowerScheduleMinutes = newSettings.PowerScheduleMinutes;
                    s.PowerScheduleAction = newSettings.PowerScheduleAction;
                    
                    // Supabase sync settings
                    s.EnableSupabaseSync = newSettings.EnableSupabaseSync;
                    s.SupabaseUrl = newSettings.SupabaseUrl;
                    s.SupabaseAnonKey = newSettings.SupabaseAnonKey;
                    s.SupabaseUserId = newSettings.SupabaseUserId;
                    s.SupabaseUploadIntervalMinutes = newSettings.SupabaseUploadIntervalMinutes;

                    // Idle timeout
                    s.IdleThresholdMinutes = newSettings.IdleThresholdMinutes;
                });

                // Handle power scheduling enable/disable
                if (PageEnablePowerSchedulingCheckBox != null)
                {
                    var isPowerSchedulingEnabled = PageEnablePowerSchedulingCheckBox.IsChecked ?? false;
                    
                    if (!isPowerSchedulingEnabled)
                    {
                        // If user disabled power scheduling, clear any active schedule
                        _powerSchedulingService.ClearSchedule();
                        System.Diagnostics.Debug.WriteLine("MainWindow: Power scheduling disabled, cleared active schedule");
                    }
                }

                // Apply window-level settings immediately
                this.Topmost = newSettings.AlwaysOnTop;
                _isMinimizeToTrayEnabled = newSettings.ShowInSystemTray;
                
                // Apply title bar visibility
                if (MainTitleBar != null)
                    MainTitleBar.Visibility = newSettings.HideTitleBar ? Visibility.Collapsed : Visibility.Visible;

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



                // Apply navigation visibility settings immediately
                ApplyNavigationVisibility(newSettings);

                // Refresh custom category navigation
                RefreshCustomCategoryNavigation();

                // Force settings to save immediately
                _settingsService?.SaveSettings();

                System.Diagnostics.Debug.WriteLine("MainWindow: All preferences applied and saved successfully");
                
                // Show success message
                ShowSaveConfirmation("Settings saved successfully");
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
                var webBrowsingContent = WebBrowsingContent;

                if (preferencesContent == null || screenTimeContent == null || webBrowsingContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - Required content panels are null");
                    return;
                }

                // Reset settings cards to initial state before showing
                ResetSettingsCardsForNextShow();

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

                // Load startup setting
                if (PageStartWithWindowsComboBox != null)
                {
                    var startupItem = PageStartWithWindowsComboBox.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == currentSettings.StartWithWindows);
                    if (startupItem != null)
                        PageStartWithWindowsComboBox.SelectedItem = startupItem;
                    else
                        PageStartWithWindowsComboBox.SelectedIndex = 0; // Default to "No"
                }

                // Load theme setting
                if (PageThemeComboBox != null)
                {
                    var themeTag = currentSettings.Theme switch
                    {
                        "Dark Theme" => "Dark",
                        _ => "Light"
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

                // Load Supabase sync settings
                if (PageEnableSupabaseSyncCheckBox != null)
                    PageEnableSupabaseSyncCheckBox.IsChecked = currentSettings.EnableSupabaseSync;
                
                if (PageSupabaseUrlTextBox != null)
                    PageSupabaseUrlTextBox.Text = currentSettings.SupabaseUrl ?? string.Empty;
                
                if (PageSupabaseAnonKeyPasswordBox != null)
                    PageSupabaseAnonKeyPasswordBox.Password = currentSettings.SupabaseAnonKey ?? string.Empty;
                
                if (PageSupabaseAnonKeyTextBox != null)
                    PageSupabaseAnonKeyTextBox.Text = currentSettings.SupabaseAnonKey ?? string.Empty;
                
                if (PageSupabaseUserIdTextBox != null)
                    PageSupabaseUserIdTextBox.Text = currentSettings.SupabaseUserId ?? string.Empty;
                
                if (PageSupabaseUploadIntervalMinutesTextBox != null)
                    PageSupabaseUploadIntervalMinutesTextBox.Value = currentSettings.SupabaseUploadIntervalMinutes > 0 
                        ? currentSettings.SupabaseUploadIntervalMinutes 
                        : 30;

                if (PageIdleThresholdMinutesTextBox != null)
                    PageIdleThresholdMinutesTextBox.Value = currentSettings.IdleThresholdMinutes >= 0
                        ? currentSettings.IdleThresholdMinutes
                        : 5;

                // Populate sound settings
                PopulatePageNotificationSoundComboBox();
                
                // Load volume setting
                if (PageNotificationVolumeSlider != null && PageVolumeValueText != null)
                {
                    var volume = currentSettings.NotificationVolume;
                    PageNotificationVolumeSlider.Value = volume;
                    PageVolumeValueText.Text = $"{volume}%";
                }

                // Load navigation visibility settings
                if (PageShowTodayTabCheckBox != null)
                    PageShowTodayTabCheckBox.IsChecked = currentSettings.ShowTodayTab;
                
                if (PageShowYesterdayTabCheckBox != null)
                    PageShowYesterdayTabCheckBox.IsChecked = currentSettings.ShowYesterdayTab;
                
                if (PageShowThisWeekTabCheckBox != null)
                    PageShowThisWeekTabCheckBox.IsChecked = currentSettings.ShowThisWeekTab;
                
                if (PageShowLastWeekTabCheckBox != null)
                    PageShowLastWeekTabCheckBox.IsChecked = currentSettings.ShowLastWeekTab;
                
                if (PageShowThisMonthTabCheckBox != null)
                    PageShowThisMonthTabCheckBox.IsChecked = currentSettings.ShowThisMonthTab;
                
                if (PageShowSleepTabCheckBox != null)
                    PageShowSleepTabCheckBox.IsChecked = currentSettings.ShowSleepTab;
                
                if (PageShowWebBrowsingTabCheckBox != null)
                    PageShowWebBrowsingTabCheckBox.IsChecked = currentSettings.ShowWebBrowsingTab;
                
                if (PageShowDevelopmentTabCheckBox != null)
                    PageShowDevelopmentTabCheckBox.IsChecked = currentSettings.ShowDevelopmentTab;
                
                if (PageShowGamingTabCheckBox != null)
                    PageShowGamingTabCheckBox.IsChecked = currentSettings.ShowGamingTab;
                
                if (PageShowCommunicationTabCheckBox != null)
                    PageShowCommunicationTabCheckBox.IsChecked = currentSettings.ShowCommunicationTab;
                
                if (PageShowProductivityTabCheckBox != null)
                    PageShowProductivityTabCheckBox.IsChecked = currentSettings.ShowProductivityTab;
                
                if (PageShowEntertainmentTabCheckBox != null)
                    PageShowEntertainmentTabCheckBox.IsChecked = currentSettings.ShowEntertainmentTab;

                // Load chart filtering settings
                if (PageShowUncategorizedInChartsCheckBox != null)
                    PageShowUncategorizedInChartsCheckBox.IsChecked = currentSettings.ShowUncategorizedInCharts;
                
                if (PageShowDevelopmentInChartsCheckBox != null)
                    PageShowDevelopmentInChartsCheckBox.IsChecked = currentSettings.ShowDevelopmentInCharts;
                
                if (PageShowGamingInChartsCheckBox != null)
                    PageShowGamingInChartsCheckBox.IsChecked = currentSettings.ShowGamingInCharts;
                
                if (PageShowCommunicationInChartsCheckBox != null)
                    PageShowCommunicationInChartsCheckBox.IsChecked = currentSettings.ShowCommunicationInCharts;
                
                if (PageShowProductivityInChartsCheckBox != null)
                    PageShowProductivityInChartsCheckBox.IsChecked = currentSettings.ShowProductivityInCharts;
                
                if (PageShowEntertainmentInChartsCheckBox != null)
                    PageShowEntertainmentInChartsCheckBox.IsChecked = currentSettings.ShowEntertainmentInCharts;
                
                if (PageShowCustomCategoriesInChartsCheckBox != null)
                    PageShowCustomCategoriesInChartsCheckBox.IsChecked = currentSettings.ShowCustomCategoriesInCharts;
                
                if (PageEnableChartAnimationsCheckBox != null)
                    PageEnableChartAnimationsCheckBox.IsChecked = currentSettings.EnableChartAnimations;

                // Load power scheduling settings
                if (PageEnablePowerSchedulingCheckBox != null)
                {
                    // Check if there's an active schedule or if power scheduling is enabled in settings
                    var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
                    var hasActiveSchedule = scheduleInfo.IsScheduled;
                    var isEnabledInSettings = currentSettings.EnablePowerScheduling;
                    
                    // Show the checkbox as enabled if there's an active schedule OR if it's enabled in settings
                    // But don't disable the checkbox - let the user control it
                    if (hasActiveSchedule || isEnabledInSettings)
                    {
                        PageEnablePowerSchedulingCheckBox.IsChecked = true;
                    }
                    else
                    {
                        // If no active schedule and not enabled in settings, show current settings state
                        // but allow user to change it
                        PageEnablePowerSchedulingCheckBox.IsChecked = isEnabledInSettings;
                    }
                }
                
                if (PagePowerScheduleHoursTextBox != null)
                    PagePowerScheduleHoursTextBox.Value = currentSettings.PowerScheduleHours;
                
                if (PagePowerScheduleMinutesTextBox != null)
                    PagePowerScheduleMinutesTextBox.Value = currentSettings.PowerScheduleMinutes;
                
                if (PagePowerScheduleActionComboBox != null)
                {
                    var actionItem = PagePowerScheduleActionComboBox.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == currentSettings.PowerScheduleAction);
                    if (actionItem != null)
                        PagePowerScheduleActionComboBox.SelectedItem = actionItem;
                    else
                        PagePowerScheduleActionComboBox.SelectedIndex = 0; // Default to "Power Off"
                }

                // Update power scheduling status display
                UpdatePowerScheduleStatusInPreferences();

                // Clear loading flag after settings are loaded
                _isLoadingPageSettings = false;

                // Ensure transforms are set up
                if (preferencesContent.RenderTransform == null)
                    preferencesContent.RenderTransform = new TranslateTransform();
                if (screenTimeContent.RenderTransform == null)
                    screenTimeContent.RenderTransform = new TranslateTransform();
                if (webBrowsingContent.RenderTransform == null)
                    webBrowsingContent.RenderTransform = new TranslateTransform();

                // Hide all content first
                screenTimeContent.Visibility = Visibility.Collapsed;
                webBrowsingContent.Visibility = Visibility.Collapsed;

                // Show preferences content with animation
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    // If web browsing is visible, fade it out first
                    if (webBrowsingContent.Visibility == Visibility.Visible)
                    {
                        Storyboard.SetTarget(fadeOutStoryboard, webBrowsingContent);
                    }
                    else
                {
                    Storyboard.SetTarget(fadeOutStoryboard, screenTimeContent);
                    }

                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        // Ensure all other content is hidden
                        screenTimeContent.Visibility = Visibility.Collapsed;
                        webBrowsingContent.Visibility = Visibility.Collapsed;
                        
                        // Show preferences
                        preferencesContent.Visibility = Visibility.Visible;
                        preferencesContent.Opacity = 0;

                        // Trigger fade-in animation for preferences content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, preferencesContent);
                            fadeInStoryboard.Begin();
                        }

                        // Trigger settings card animations after the page transition
                        TriggerSettingsCardAnimations();
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    screenTimeContent.Visibility = Visibility.Collapsed;
                    webBrowsingContent.Visibility = Visibility.Collapsed;
                    preferencesContent.Visibility = Visibility.Visible;
                    TriggerSettingsCardAnimations();
                }

                System.Diagnostics.Debug.WriteLine("MainWindow: Preferences page shown with current settings loaded and animations triggered");
                UpdateCustomCategoryTogglesPanel(currentSettings);
            }
            catch (Exception ex)
            {
                _isLoadingPageSettings = false; // Ensure flag is cleared on error
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing preferences page: {ex.Message}");
            }
        }

        private void HidePreferencesPage()
        {
            try
            {
                var preferencesContent = PreferencesContent;
                var screenTimeContent = ScreenTimeContent;
                var webBrowsingContent = WebBrowsingContent;

                if (preferencesContent == null || screenTimeContent == null || webBrowsingContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - Required content panels are null");
                    return;
                }

                // Reset settings cards to initial state for next time
                ResetSettingsCardsForNextShow();

                // Ensure transforms are set up
                if (preferencesContent.RenderTransform == null)
                    preferencesContent.RenderTransform = new TranslateTransform();
                if (screenTimeContent.RenderTransform == null)
                    screenTimeContent.RenderTransform = new TranslateTransform();
                if (webBrowsingContent.RenderTransform == null)
                    webBrowsingContent.RenderTransform = new TranslateTransform();

                // Hide all content first
                webBrowsingContent.Visibility = Visibility.Collapsed;
                screenTimeContent.Visibility = Visibility.Collapsed;

                // Animate the transition back to main content
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, preferencesContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        preferencesContent.Visibility = Visibility.Collapsed;
                        screenTimeContent.Visibility = Visibility.Visible;
                        screenTimeContent.Opacity = 0;
                        
                        // Reset transforms
                        if (screenTimeContent.RenderTransform is TranslateTransform screenTransform)
                            screenTransform.Y = 0;
                        
                        // Trigger fade-in animation for main content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, screenTimeContent);
                            fadeInStoryboard.Begin();
                        }

                        // Refresh the app list to ensure it's up to date
                        RefreshAppList();
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    preferencesContent.Visibility = Visibility.Collapsed;
                    webBrowsingContent.Visibility = Visibility.Collapsed;
                    screenTimeContent.Visibility = Visibility.Visible;
                    RefreshAppList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error hiding preferences page: {ex.Message}");
            }
        }

        private void TriggerSettingsCardAnimations()
        {
            try
            {
                // Find the settings cards and trigger staggered animations
                var systemCard = this.FindName("SystemSettingsCard") as Wpf.Ui.Controls.Card;
                var notificationsCard = this.FindName("NotificationsSettingsCard") as Wpf.Ui.Controls.Card;
                var navigationCard = this.FindName("NavigationSettingsCard") as Wpf.Ui.Controls.Card;
                var chartCard = this.FindName("ChartSettingsCard") as Wpf.Ui.Controls.Card;

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

                if (navigationCard != null)
                {
                    var storyboard3 = this.FindResource("SettingsCardFloatInAnimation") as Storyboard;
                    if (storyboard3 != null)
                    {
                        Storyboard.SetTarget(storyboard3, navigationCard);
                        storyboard3.Begin();
                    }
                }

                if (chartCard != null)
                {
                    var storyboard4 = this.FindResource("SettingsCardFloatInAnimation") as Storyboard;
                    if (storyboard4 != null)
                    {
                        Storyboard.SetTarget(storyboard4, chartCard);
                        storyboard4.Begin();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error triggering settings card animations: {ex.Message}");
            }
        }

        private void ResetSettingsCardsForNextShow()
        {
            try
            {
                var systemCard = this.FindName("SystemSettingsCard") as Wpf.Ui.Controls.Card;
                var notificationsCard = this.FindName("NotificationsSettingsCard") as Wpf.Ui.Controls.Card;
                var navigationCard = this.FindName("NavigationSettingsCard") as Wpf.Ui.Controls.Card;
                var chartCard = this.FindName("ChartSettingsCard") as Wpf.Ui.Controls.Card;

                // Reset system card
                if (systemCard != null)
                {
                    systemCard.Opacity = 0;
                    if (systemCard.RenderTransform is TransformGroup systemTransform)
                    {
                        var systemTranslate = systemTransform.Children.OfType<TranslateTransform>().FirstOrDefault();
                        var systemScale = systemTransform.Children.OfType<ScaleTransform>().FirstOrDefault();
                        
                        if (systemTranslate != null)
                            systemTranslate.Y = 40;
                        if (systemScale != null)
                        {
                            systemScale.ScaleX = 0.95;
                            systemScale.ScaleY = 0.95;
                        }
                    }
                }

                // Reset notifications card
                if (notificationsCard != null)
                {
                    notificationsCard.Opacity = 0;
                    if (notificationsCard.RenderTransform is TransformGroup notificationsTransform)
                    {
                        var notificationsTranslate = notificationsTransform.Children.OfType<TranslateTransform>().FirstOrDefault();
                        var notificationsScale = notificationsTransform.Children.OfType<ScaleTransform>().FirstOrDefault();
                        
                        if (notificationsTranslate != null)
                            notificationsTranslate.Y = 40;
                        if (notificationsScale != null)
                        {
                            notificationsScale.ScaleX = 0.95;
                            notificationsScale.ScaleY = 0.95;
                        }
                    }
                }

                // Reset navigation card
                if (navigationCard != null)
                {
                    navigationCard.Opacity = 0;
                    if (navigationCard.RenderTransform is TransformGroup navigationTransform)
                    {
                        var navigationTranslate = navigationTransform.Children.OfType<TranslateTransform>().FirstOrDefault();
                        var navigationScale = navigationTransform.Children.OfType<ScaleTransform>().FirstOrDefault();
                        
                        if (navigationTranslate != null)
                            navigationTranslate.Y = 40;
                        if (navigationScale != null)
                        {
                            navigationScale.ScaleX = 0.95;
                            navigationScale.ScaleY = 0.95;
                        }
                    }
                }

                // Reset chart card
                if (chartCard != null)
                {
                    chartCard.Opacity = 0;
                    if (chartCard.RenderTransform is TransformGroup chartTransform)
                    {
                        var chartTranslate = chartTransform.Children.OfType<TranslateTransform>().FirstOrDefault();
                        var chartScale = chartTransform.Children.OfType<ScaleTransform>().FirstOrDefault();
                        
                        if (chartTranslate != null)
                            chartTranslate.Y = 40;
                        if (chartScale != null)
                        {
                            chartScale.ScaleX = 0.95;
                            chartScale.ScaleY = 0.95;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting settings cards: {ex.Message}");
            }
        }

        private void PageThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingPageSettings) return; // Don't apply theme changes during loading

            if (sender is ComboBox comboBox && comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                string? theme = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(theme))
                {
                    ApplySavedTheme(theme);
                }
            }
        }

        private void PageStartWithWindowsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!_isLoadingPageSettings && sender is ComboBox comboBox && 
                    comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
                {
                    string? startupOption = selectedItem.Tag?.ToString();
                    if (!string.IsNullOrEmpty(startupOption))
                    {
                        // Save the startup setting
                        _settingsService?.UpdateSettings(s => s.StartWithWindows = startupOption);
                        System.Diagnostics.Debug.WriteLine($"Startup setting changed to: {startupOption}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error changing startup setting: {ex.Message}");
            }
        }

        private void PageHideTitleBarCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingPageSettings) return;

            try
            {
                // Update the title bar visibility immediately
                if (MainTitleBar != null)
                    MainTitleBar.Visibility = Visibility.Collapsed;
                
                // Update the menu item
                if (HideTitleBarMenuItem != null)
                    HideTitleBarMenuItem.IsChecked = true;
                
                // Update working settings
                if (_workingPageSettings != null)
                    _workingPageSettings.HideTitleBar = true;
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Hide title bar enabled from preferences");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error enabling hide title bar: {ex.Message}");
            }
        }

        private void PageHideTitleBarCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingPageSettings) return;

            try
            {
                // Update the title bar visibility immediately
                if (MainTitleBar != null)
                    MainTitleBar.Visibility = Visibility.Visible;
                
                // Update the menu item
                if (HideTitleBarMenuItem != null)
                    HideTitleBarMenuItem.IsChecked = false;
                
                // Update working settings
                if (_workingPageSettings != null)
                    _workingPageSettings.HideTitleBar = false;
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Hide title bar disabled from preferences");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error disabling hide title bar: {ex.Message}");
            }
        }

        private async void ShowLiveDashboard_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Live dashboard feature is coming soon!");
        }

        private async void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show export options dialog
                var result = await ShowContentDialogAsync(
                    "Export Data",
                    "Choose what data you would like to export:\n\n" +
                    "• Apps Only: Summary of app usage\n" +
                    "• Apps (Detailed): Daily breakdown of app usage\n" +
                    "• All Data: Combined data with daily breakdown",
                    "Apps Only",
                    "Apps (Detailed)",
                    "All Data"
                );

                string exportType = "";
                string exportDescription = "";

                switch (result)
                {
                    case Wpf.Ui.Controls.ContentDialogResult.Primary: // Apps Only
                        exportType = "Apps Only";
                        exportDescription = "Summary of app usage data";
                        break;
                    case Wpf.Ui.Controls.ContentDialogResult.Secondary: // Apps (Detailed)
                        exportType = "Apps (Detailed)";
                        exportDescription = "Daily breakdown of app usage data";
                        break;
                    case Wpf.Ui.Controls.ContentDialogResult.None: // All Data
                        exportType = "All Data";
                        exportDescription = "Combined app and website data with daily breakdown";
                        break;
                    default:
                        return; // User cancelled
                }

                // Show confirmation dialog
                var confirmResult = await ShowContentDialogAsync(
                    "Confirm Export",
                    $"You are about to export: {exportType}\n\n" +
                    $"This will export {exportDescription} to a CSV file.\n\n" +
                    "Do you want to proceed?",
                    "Export",
                    "Cancel"
                );

                if (confirmResult != Wpf.Ui.Controls.ContentDialogResult.Primary)
                {
                    return; // User cancelled
                }

                bool exportSuccess = false;

                switch (result)
                {
                    case Wpf.Ui.Controls.ContentDialogResult.Primary: // Apps Only
                        exportSuccess = await _exportService.ExportAppsToCSVAsync();
                        break;
                    case Wpf.Ui.Controls.ContentDialogResult.Secondary: // Apps (Detailed)
                        exportSuccess = await _exportService.ExportDetailedAppsToCSVAsync();
                        break;
                    case Wpf.Ui.Controls.ContentDialogResult.None: // All Data
                        exportSuccess = await _exportService.ExportAllDataToCSVAsync();
                        break;
                }

                if (exportSuccess)
                {
                    await ShowInfoDialogAsync("Export Successful", "Your data has been exported successfully!");
                }
                else
                {
                    await ShowErrorDialogAsync("Export Failed", "Failed to export data. Please try again.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error in ExportCSV_Click: {ex.Message}");
                await ShowErrorDialogAsync("Export Error", $"An error occurred while exporting: {ex.Message}");
            }
        }

        private async void ExportCharts_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Export charts feature is coming soon!");
        }

        private async void ExportWebsites_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show website export options dialog
                var result = await ShowContentDialogAsync(
                    "Export Website Data",
                    "Choose website export format:\n\n" +
                    "• Summary: Total usage per website\n" +
                    "• Detailed: Daily breakdown per website",
                    "Summary",
                    "Detailed"
                );

                string exportType = "";
                string exportDescription = "";

                switch (result)
                {
                    case Wpf.Ui.Controls.ContentDialogResult.Primary: // Summary
                        exportType = "Website Summary";
                        exportDescription = "Total usage per website";
                        break;
                    case Wpf.Ui.Controls.ContentDialogResult.Secondary: // Detailed
                        exportType = "Website Detailed";
                        exportDescription = "Daily breakdown per website";
                        break;
                    default:
                        return; // User cancelled
                }

                // Show confirmation dialog
                var confirmResult = await ShowContentDialogAsync(
                    "Confirm Export",
                    $"You are about to export: {exportType}\n\n" +
                    $"This will export {exportDescription} to a CSV file.\n\n" +
                    "Do you want to proceed?",
                    "Export",
                    "Cancel"
                );

                if (confirmResult != Wpf.Ui.Controls.ContentDialogResult.Primary)
                {
                    return; // User cancelled
                }

                bool exportSuccess = false;

                switch (result)
                {
                    case Wpf.Ui.Controls.ContentDialogResult.Primary: // Summary
                        exportSuccess = await _exportService.ExportWebsitesToCSVAsync();
                        break;
                    case Wpf.Ui.Controls.ContentDialogResult.Secondary: // Detailed
                        exportSuccess = await _exportService.ExportDetailedWebsitesToCSVAsync();
                        break;
                }

                if (exportSuccess)
                {
                    await ShowInfoDialogAsync("Export Successful", "Your website data has been exported successfully!");
                }
                else
                {
                    await ShowErrorDialogAsync("Export Failed", "Failed to export website data. Please try again.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error in ExportWebsites_Click: {ex.Message}");
                await ShowErrorDialogAsync("Export Error", $"An error occurred while exporting website data: {ex.Message}");
            }
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

        private async void ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            await ShowInfoDialogAsync("Coming Soon", "Bar chart feature is coming soon!");
        }

        private async void ManageCategories_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var categoryService = _screenTimeService.GetCategoryService();
                var categoryWindow = new Windows.CategoryManagementWindow(categoryService, _screenTimeService);
                categoryWindow.Owner = this;
                
                if (categoryWindow.ShowDialog() == true)
                {
                    // Refresh the app list to show updated categories
                    RefreshAppList();
                    UpdateNavigationStats();
                    
                    // If we have a current category filter, make sure it's still valid
                    if (!string.IsNullOrEmpty(_currentCategoryFilter))
                    {
                        var categories = categoryService.GetAllCategories();
                        if (!categories.Contains(_currentCategoryFilter))
                        {
                            // Category was deleted, clear the filter
                            ClearCategoryFilter();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error in ManageCategories_Click: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to open category management: {ex.Message}");
            }
        }

        private async void ViewByCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var categoryService = _screenTimeService.GetCategoryService();
                var categories = categoryService.GetAllCategories().ToList();
                
                if (!categories.Any())
                {
                    await ShowInfoDialogAsync("No Categories", "No categories are available. Please create some categories first.");
                    return;
                }

                var dialog = new Windows.CategorySelectionDialog(categories, "Select Category to View");
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedCategory))
                {
                    var selectedCategory = dialog.SelectedCategory;
                    
                    // Find the NavigationViewItem with matching Tag
                    var navItem = FindNavigationViewItemByTag(selectedCategory);
                    if (navItem != null)
                    {
                        // Trigger the click handler directly to apply the category filter
                        FilterByCategory_Click(navItem, new RoutedEventArgs());
                    }
                    else
                    {
                        // Fallback: manually set category filter and refresh
                        _currentCategoryFilter = selectedCategory;
                        UpdateLabelsForCurrentContext();
                        RefreshAppList();
                        UpdateNavigationStats();
                        UpdateCategoryFilterButtonVisibility();
                        
                        await ShowInfoDialogAsync("Category Filter Applied", 
                            $"Showing apps in the '{selectedCategory}' category. Use 'Clear Category Filter' to remove the filter.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error in ViewByCategory_Click: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to view by category: {ex.Message}");
            }
        }

        // Helper to find NavigationViewItem by Tag
        private Wpf.Ui.Controls.NavigationViewItem? FindNavigationViewItemByTag(string tag)
        {
            foreach (var item in MainNavigationView.MenuItems)
            {
                if (item is Wpf.Ui.Controls.NavigationViewItem navItem && navItem.Tag?.ToString() == tag)
                    return navItem;
            }
            return null;
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
            try
            {
                // Toggle the title bar visibility
                var currentVisibility = MainTitleBar?.Visibility ?? Visibility.Visible;
                var newValue = currentVisibility != Visibility.Visible;
                
                // Update the title bar visibility
                if (MainTitleBar != null)
                    MainTitleBar.Visibility = newValue ? Visibility.Collapsed : Visibility.Visible;
                
                // Update the settings
                _settingsService.UpdateSettings(s => s.HideTitleBar = newValue);
                
                // Update the menu item checked state
                if (HideTitleBarMenuItem != null)
                    HideTitleBarMenuItem.IsChecked = newValue;
                
                // Update the preferences page checkbox if it's visible
                if (PageHideTitleBarCheckBox != null)
                    PageHideTitleBarCheckBox.IsChecked = newValue;
                
                System.Diagnostics.Debug.WriteLine($"MainWindow: Hide title bar toggled to: {newValue}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error toggling hide title bar: {ex.Message}");
            }
        }

        private void StartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Cycle through the options: No -> Yes -> Minimized -> No
                var currentSetting = _settingsService.CurrentSettings.StartWithWindows;
                var newSetting = currentSetting switch
                {
                    "No" => "Yes",
                    "Yes" => "Minimized",
                    "Minimized" => "No",
                    _ => "No"
                };
                
                // Update the setting
                _settingsService.UpdateSettings(s => s.StartWithWindows = newSetting);
                
                // The SettingsService will automatically sync with the registry
                System.Diagnostics.Debug.WriteLine($"Startup setting cycled to: {newSetting}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error cycling startup with Windows: {ex.Message}");
                // Show error to user
                MessageBox.Show($"Failed to update startup setting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private async void SchedulePowerAction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Windows.PowerSchedulingDialog(_powerSchedulingService);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing power scheduling dialog: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to open power scheduling dialog: {ex.Message}");
            }
        }

        private async void CancelCurrentPowerSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
                
                if (!scheduleInfo.IsScheduled)
                {
                    await ShowInfoDialogAsync("No Schedule", "There is no active power schedule to cancel.");
                    return;
                }

                var actionText = scheduleInfo.Action == PowerSchedulingService.PowerAction.Shutdown ? "power off" : "restart";
                var timeText = FormatTimeSpan(scheduleInfo.TimeRemaining);
                
                var confirmed = await ShowConfirmationDialogAsync(
                    "Cancel Power Schedule",
                    $"Are you sure you want to cancel the scheduled {actionText} in {timeText}?"
                );

                if (confirmed)
                {
                    _powerSchedulingService.ClearSchedule();
                    await ShowInfoDialogAsync("Schedule Cancelled", "The power schedule has been cancelled successfully.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error cancelling power schedule: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to cancel power schedule: {ex.Message}");
            }
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
            try
            {
                // Create a popup layer Grid that overlays the entire window
                var popupLayerGrid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
                };

                // Create content presenter for the dialog and add it to the popup layer
                var contentPresenter = new System.Windows.Controls.ContentPresenter();
                popupLayerGrid.Children.Add(contentPresenter);

                // Create a popup window that covers the main window
                var popup = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Owner = this,
                    Content = popupLayerGrid,
                    Width = this.ActualWidth,
                    Height = this.ActualHeight,
                    Left = this.Left,
                    Top = this.Top,
                    WindowState = this.WindowState,
                    Topmost = true
                };

                var dialog = new Wpf.Ui.Controls.ContentDialog
                {
                    Title = "About Chronos",
                    CloseButtonText = "Close",
                    DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Close,
                    DialogHeight = 300,
                    DialogWidth = 400,
                    DialogMaxWidth = 400,
                    DialogMaxHeight = 300,
                    DialogMargin = new Thickness(16),
                    Content = new System.Windows.Controls.TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14,
                        Margin = new Thickness(0, 10, 0, 10),
                        Inlines = 
                        {
                            new Bold(new Run("Chronos Screen Time Tracker\n")),
                            new Run("Version 2.1.0"),
                            new Run("\nA modern, screen time tracking application made to save you from your screen.\n\n"),
                            new Run("Made with love by "),
                            new Hyperlink(new Run("Ghassan Elgendy"))
                            {
                                NavigateUri = new Uri("https://github.com/ghassanelgendy"),
                                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212))
                            },
                            new Run("\n\n"),
                            new Run("(C) 2025 Ghassan Elgendy. All rights reserved.")
                        }
                    },
                    DialogHost = contentPresenter
                };

                // Handle hyperlink navigation
                if (dialog.Content is System.Windows.Controls.TextBlock textBlock)
                {
                    foreach (var inline in textBlock.Inlines.OfType<Hyperlink>())
                    {
                        inline.RequestNavigate += Hyperlink_RequestNavigate;
                    }
                }

                // Handle window state changes
                this.LocationChanged += (s, e) =>
                {
                    popup.Left = this.Left;
                    popup.Top = this.Top;
                };

                this.SizeChanged += (s, e) =>
                {
                    popup.Width = this.ActualWidth;
                    popup.Height = this.ActualHeight;
                    popup.WindowState = this.WindowState;
                };

                // Show the popup
                popup.Show();

                try
                {
                    await dialog.ShowAsync();
                }
                finally
                {
                    // Clean up
                    this.LocationChanged -= (s, e) =>
                    {
                        popup.Left = this.Left;
                        popup.Top = this.Top;
                    };

                    this.SizeChanged -= (s, e) =>
                    {
                        popup.Width = this.ActualWidth;
                        popup.Height = this.ActualHeight;
                        popup.WindowState = this.WindowState;
                    };

                    popup.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing about dialog: {ex.Message}");
                MessageBox.Show($"Error showing about dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<bool> ShowUpdateDialogAsync(chronos_screentime.Services.UpdateInfo updateInfo)
        {
            try
            {
                // Create a popup layer Grid that overlays the entire window
                var popupLayerGrid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
                };

                // Create content presenter for the dialog and add it to the popup layer
                var contentPresenter = new System.Windows.Controls.ContentPresenter();
                popupLayerGrid.Children.Add(contentPresenter);

                // Create a popup window that covers the main window
                var popup = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Owner = this,
                    Content = popupLayerGrid,
                    Width = this.ActualWidth,
                    Height = this.ActualHeight,
                    Left = this.Left,
                    Top = this.Top,
                    WindowState = this.WindowState,
                    Topmost = true
                };

                var dialog = new Wpf.Ui.Controls.ContentDialog
                {
                    Title = "Update Available",
                    PrimaryButtonText = "Update Now",
                    SecondaryButtonText = "Later",
                    DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Primary,
                    DialogHeight = 400,
                    DialogWidth = 500,
                    DialogMaxWidth = 500,
                    DialogMaxHeight = 400,
                    DialogMargin = new Thickness(16),
                    Content = new System.Windows.Controls.TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14,
                        Margin = new Thickness(0, 10, 0, 10),
                        Inlines = 
                        {
                            new Bold(new Run($"Version {updateInfo.Version} is available!")),
                            new Run("\n\n"),
                            new Run("Release Notes:\n"),
                            new Run(updateInfo.ReleaseNotes),
                            new Run("\n\n"),
                            new Run($"Release Date: {updateInfo.ReleaseDate:MMM dd, yyyy}"),
                            new Run("\n\n"),
                            new Run("Would you like to download and install this update now?")
                        }
                    },
                    DialogHost = contentPresenter
                };

                // Handle window state changes
                this.LocationChanged += (s, e) =>
                {
                    popup.Left = this.Left;
                    popup.Top = this.Top;
                };

                this.SizeChanged += (s, e) =>
                {
                    popup.Width = this.ActualWidth;
                    popup.Height = this.ActualHeight;
                    popup.WindowState = this.WindowState;
                };

                // Show the popup
                popup.Show();

                try
                {
                    var result = await dialog.ShowAsync();
                    
                    // Record the user's choice for future suppression
                    if (result == Wpf.Ui.Controls.ContentDialogResult.Primary)
                    {
                        // User clicked "Update Now" - no suppression needed
                        return true;
                    }
                    else if (result == Wpf.Ui.Controls.ContentDialogResult.Secondary)
                    {
                        // User clicked "Later" - record dismissal
                        Services.UpdateService.RecordUpdateDismissed();
                        return false;
                    }
                    else
                    {
                        // User closed dialog or clicked close button - record cancellation
                        Services.UpdateService.RecordUpdateCancelled();
                        return false;
                    }
                }
                finally
                {
                    // Clean up
                    this.LocationChanged -= (s, e) =>
                    {
                        popup.Left = this.Left;
                        popup.Top = this.Top;
                    };

                    this.SizeChanged -= (s, e) =>
                    {
                        popup.Width = this.ActualWidth;
                        popup.Height = this.ActualHeight;
                        popup.WindowState = this.WindowState;
                    };

                    popup.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing update dialog: {ex.Message}");
                MessageBox.Show($"Error showing update dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
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



    
        #endregion

        #region Date Navigation Methods

        private void ShowToday_Click(object sender, RoutedEventArgs e)
        {
            _isTimeRangeChange = true;
            _currentPeriod = "Today";
            UpdateLabelsForCurrentContext();
            UpdateCategoryFilterButtonVisibility();
            RefreshAppList();
            UpdateNavigationStats();
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
                    var webBrowsingContent = WebBrowsingContent;
                    var sleepContent = SleepContent;

                    if (preferencesContent != null && screenTimeContent != null)
                    {
                        preferencesContent.Visibility = Visibility.Collapsed;
                        screenTimeContent.Visibility = Visibility.Visible;
                    }

                    if (webBrowsingContent != null)
                    {
                        webBrowsingContent.Visibility = Visibility.Collapsed;
                    }

                    if (sleepContent != null)
                    {
                        sleepContent.Visibility = Visibility.Collapsed;
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
            _isTimeRangeChange = true;
            _currentPeriod = "Yesterday";
            UpdateLabelsForCurrentContext();
            UpdateCategoryFilterButtonVisibility();
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowThisWeek_Click(object sender, RoutedEventArgs e)
        {
            _isTimeRangeChange = true;
            _currentPeriod = "This Week";
            UpdateLabelsForCurrentContext();
            UpdateCategoryFilterButtonVisibility();
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowLastWeek_Click(object sender, RoutedEventArgs e)
        {
            _isTimeRangeChange = true;
            _currentPeriod = "Last Week";
            UpdateLabelsForCurrentContext();
            UpdateCategoryFilterButtonVisibility();
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowThisMonth_Click(object sender, RoutedEventArgs e)
        {
            _isTimeRangeChange = true;
            _currentPeriod = "This Month";
            UpdateLabelsForCurrentContext();
            UpdateCategoryFilterButtonVisibility();
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowCustomRange_Click(object sender, RoutedEventArgs e)
        {
            _isTimeRangeChange = true;
            _currentPeriod = "Custom Range";
            UpdateLabelsForCurrentContext();
            UpdateCategoryFilterButtonVisibility();
            RefreshAppList();
            UpdateNavigationStats();
        }

        private void ShowSleep_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if required UI elements exist
                if (SleepContent == null || ScreenTimeContent == null || PreferencesContent == null || WebBrowsingContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Required UI elements are null");
                    return;
                }

                // Ensure transforms are set up
                if (SleepContent.RenderTransform == null)
                    SleepContent.RenderTransform = new TranslateTransform();
                if (ScreenTimeContent.RenderTransform == null)
                    ScreenTimeContent.RenderTransform = new TranslateTransform();

                // Animate the transition
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, ScreenTimeContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        ScreenTimeContent.Visibility = Visibility.Collapsed;
                        PreferencesContent.Visibility = Visibility.Collapsed;
                        WebBrowsingContent.Visibility = Visibility.Collapsed;
                        SleepContent.Visibility = Visibility.Visible;
                        SleepContent.Opacity = 0;

                        // Trigger fade-in animation for sleep content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, SleepContent);
                            fadeInStoryboard.Begin();
                        }

                        // Refresh sleep data
                        RefreshSleepData();
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    SleepContent.Visibility = Visibility.Visible;
                    ScreenTimeContent.Visibility = Visibility.Collapsed;
                    PreferencesContent.Visibility = Visibility.Collapsed;
                    WebBrowsingContent.Visibility = Visibility.Collapsed;
                    RefreshSleepData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing sleep page: {ex.Message}");
                MessageBox.Show($"Error showing sleep page: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowCharts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if required UI elements exist
                if (ChartsContent == null || ScreenTimeContent == null || PreferencesContent == null || WebBrowsingContent == null || SleepContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Required UI elements are null");
                    return;
                }

                // Ensure transforms are set up
                if (ChartsContent.RenderTransform == null)
                    ChartsContent.RenderTransform = new TranslateTransform();
                if (ScreenTimeContent.RenderTransform == null)
                    ScreenTimeContent.RenderTransform = new TranslateTransform();

                // Animate the transition
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, ScreenTimeContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        ScreenTimeContent.Visibility = Visibility.Collapsed;
                        PreferencesContent.Visibility = Visibility.Collapsed;
                        WebBrowsingContent.Visibility = Visibility.Collapsed;
                        SleepContent.Visibility = Visibility.Collapsed;
                        ChartsContent.Visibility = Visibility.Visible;
                        ChartsContent.Opacity = 0;

                        // Trigger fade-in animation for charts content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, ChartsContent);
                            fadeInStoryboard.Begin();
                        }

                        // Refresh chart data
                        RefreshChartData();
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    ChartsContent.Visibility = Visibility.Visible;
                    ScreenTimeContent.Visibility = Visibility.Collapsed;
                    PreferencesContent.Visibility = Visibility.Collapsed;
                    WebBrowsingContent.Visibility = Visibility.Collapsed;
                    SleepContent.Visibility = Visibility.Collapsed;
                    RefreshChartData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing charts page: {ex.Message}");
                MessageBox.Show($"Error showing charts page: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    // Handle custom category tags (remove "Custom_" prefix)
                    if (category.StartsWith("Custom_"))
                    {
                        category = category.Substring("Custom_".Length);
                    }

                    // Check if we need to go back first (if we're in Web Browsing, Preferences, or Sleep)
                    bool needsBackNavigation = false;
                    
                    if ((WebBrowsingContent?.Visibility == Visibility.Visible) || 
                        (PreferencesContent?.Visibility == Visibility.Visible) ||
                        (SleepContent?.Visibility == Visibility.Visible))
                    {
                        needsBackNavigation = true;
                    }

                    if (needsBackNavigation)
                    {
                        // Simulate back navigation first
                        BackToMain_Click(this, new RoutedEventArgs());
                    }

                    // Now handle the new navigation
                    if (category == "WebBrowsing")
                    {
                        // Clear category filter for web browsing
                        _currentCategoryFilter = null;
                        
                        // Check if required UI elements exist
                        if (WebBrowsingContent == null || ScreenTimeContent == null || PreferencesContent == null)
                        {
                            System.Diagnostics.Debug.WriteLine("MainWindow: Required UI elements are null");
                            return;
                        }

                        // Ensure transforms are set up
                        if (WebBrowsingContent.RenderTransform == null)
                            WebBrowsingContent.RenderTransform = new TranslateTransform();
                        if (ScreenTimeContent.RenderTransform == null)
                            ScreenTimeContent.RenderTransform = new TranslateTransform();

                        // Animate the transition
                        var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                        if (fadeOutStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeOutStoryboard, ScreenTimeContent);
                            fadeOutStoryboard.Completed += (s, e) =>
                            {
                                ScreenTimeContent.Visibility = Visibility.Collapsed;
                                PreferencesContent.Visibility = Visibility.Collapsed;
                                WebBrowsingContent.Visibility = Visibility.Visible;
                                WebBrowsingContent.Opacity = 0;

                                // Trigger fade-in animation for web browsing content
                                var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                                if (fadeInStoryboard != null)
                                {
                                    Storyboard.SetTarget(fadeInStoryboard, WebBrowsingContent);
                                    fadeInStoryboard.Begin();
                                }

                                // Refresh web browsing data
                                RefreshWebsiteList();
                                UpdateWebBrowsingStats();
                            };
                            fadeOutStoryboard.Begin();
                        }
                        else
                        {
                            // Fallback without animation
                            WebBrowsingContent.Visibility = Visibility.Visible;
                            ScreenTimeContent.Visibility = Visibility.Collapsed;
                            PreferencesContent.Visibility = Visibility.Collapsed;
                            RefreshWebsiteList();
                            UpdateWebBrowsingStats();
                        }
                    }
                    else
                    {
                        // Set category filter
                        _currentCategoryFilter = category;
                        
                        // Show main content and hide others
                        if (WebBrowsingContent != null)
                            WebBrowsingContent.Visibility = Visibility.Collapsed;
                        if (ScreenTimeContent != null)
                            ScreenTimeContent.Visibility = Visibility.Visible;
                        if (PreferencesContent != null)
                            PreferencesContent.Visibility = Visibility.Collapsed;
                        if (SleepContent != null)
                            SleepContent.Visibility = Visibility.Collapsed;
                            
                        // Update labels for the current context
                        UpdateLabelsForCurrentContext();
                        
                        // Show the clear category filter button
                        UpdateCategoryFilterButtonVisibility();
                        
                        // Filter apps by category
                        var appsInCategory = _screenTimeService.GetAppsByCategory(category).ToList();
                        if (appsInCategory.Any())
                        {
                            _isTimeRangeChange = true;
                            RefreshAppList();
                            UpdateNavigationStats();
                        }
                        else
                        {
                            // No apps in this category, show all apps but indicate the filter
                            _isTimeRangeChange = true;
                            RefreshAppList();
                            UpdateNavigationStats();
                        }
                    }
                }
            }
        }

        #endregion

        #region Web Browsing Methods

        private void ShowWebBrowsingPage()
        {
            try
            {
                var webBrowsingContent = WebBrowsingContent;
                var screenTimeContent = ScreenTimeContent;
                var preferencesContent = PreferencesContent;

                if (webBrowsingContent == null || screenTimeContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - WebBrowsingContent or ScreenTimeContent is null");
                    return;
                }

                // Hide other content panels
                if (preferencesContent != null)
                    preferencesContent.Visibility = Visibility.Collapsed;
                screenTimeContent.Visibility = Visibility.Collapsed;

                // Show web browsing content
                webBrowsingContent.Visibility = Visibility.Visible;

                // Initial data refresh
                RefreshWebsiteList();
                UpdateWebBrowsingStats();

                System.Diagnostics.Debug.WriteLine("MainWindow: Web Browsing page shown successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing web browsing page: {ex.Message}");
                MessageBox.Show($"Error showing web browsing page: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshWebsites_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshWebsiteList();
                UpdateWebBrowsingStats();
                System.Diagnostics.Debug.WriteLine("MainWindow: Website data refreshed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error refreshing websites: {ex.Message}");
                MessageBox.Show($"Error refreshing website data: {ex.Message}", "Refresh Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ResetAllWebsites_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = await ShowConfirmationDialogAsync(
                    "Reset All Website Data", 
                    "Are you sure you want to reset all website tracking data? This action cannot be undone.");

                if (result)
                {
                    _screenTimeService.ResetAllWebsiteData();
                    RefreshWebsiteList();
                    UpdateWebBrowsingStats();
                    System.Diagnostics.Debug.WriteLine("MainWindow: All website data reset");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error resetting all website data: {ex.Message}");
                await ShowErrorDialogAsync("Reset Error", $"Failed to reset website data: {ex.Message}");
            }
        }

        private async void ResetWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Tag is string domain)
                {
                    var result = await ShowConfirmationDialogAsync(
                        "Reset Website Data", 
                        $"Are you sure you want to reset data for {domain}? This action cannot be undone.");

                    if (result)
                    {
                        var website = _screenTimeService.GetWebsite(domain);
                        if (website != null)
                        {
                            website.TotalTime = TimeSpan.Zero;
                            website.SessionCount = 0;
                            website.DailyTimes.Clear();
                            website.DailySessions.Clear();
                            
                            RefreshWebsiteList();
                            UpdateWebBrowsingStats();
                            System.Diagnostics.Debug.WriteLine($"MainWindow: Website data reset for {domain}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error resetting website data: {ex.Message}");
                await ShowErrorDialogAsync("Reset Error", $"Failed to reset website data: {ex.Message}");
            }
        }

        private string GetFormattedTimeShortForWebsite(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours}h {time.Minutes}m";
            }
            else if (time.TotalMinutes >= 1)
            {
                return $"{(int)time.TotalMinutes}m {time.Seconds}s";
            }
            else
            {
                return $"{time.Seconds}s";
            }
        }

        private void RefreshWebsiteList()
        {
            try
            {
                var today = DateTime.Today;
                var websites = _screenTimeService.GetAllWebsites().ToList();
                List<WebsiteScreenTime> filteredWebsites = new();

                switch (_currentPeriod)
                {
                    case "Today":
                        filteredWebsites = websites.Where(w => w.DailyTimes.ContainsKey(today)).ToList();
                        break;

                    case "Yesterday":
                        var yesterday = today.AddDays(-1);
                        filteredWebsites = websites.Where(w => w.DailyTimes.ContainsKey(yesterday)).ToList();
                        break;

                    case "This Week":
                        var weekStart = today.AddDays(-(int)today.DayOfWeek);
                        filteredWebsites = websites.Where(w => w.DailyTimes.Any(dt => 
                            dt.Key >= weekStart && dt.Key <= today)).ToList();
                        break;

                    case "Last Week":
                        var lastWeekStart = today.AddDays(-(int)today.DayOfWeek - 7);
                        var lastWeekEnd = lastWeekStart.AddDays(6);
                        filteredWebsites = websites.Where(w => w.DailyTimes.Any(dt => 
                            dt.Key >= lastWeekStart && dt.Key <= lastWeekEnd)).ToList();
                        break;

                    case "This Month":
                        var monthStart = new DateTime(today.Year, today.Month, 1);
                        filteredWebsites = websites.Where(w => w.DailyTimes.Any(dt => 
                            dt.Key >= monthStart && dt.Key <= today)).ToList();
                        break;

                    default:
                        filteredWebsites = websites;
                        break;
                }

                // Create UI-friendly website data with additional properties
                var websiteDisplayData = filteredWebsites.Select(w => new
                {
                    Domain = w.Domain,
                    DisplayName = w.DisplayName,
                    TotalTime = w.TotalTime,
                    FormattedTotalTimeShort = GetFormattedTimeShortForWebsite(GetTimeForCurrentPeriod(w)),
                    TodaysSessionCount = w.TodaysSessionCount,
                    LastSeen = w.DailyTimes.Keys.Any() ? w.DailyTimes.Keys.Max() : DateTime.MinValue,
                    FaviconUrl = w.FaviconUrl
                }).OrderByDescending(w => GetTimeForCurrentPeriod(filteredWebsites.First(fw => fw.Domain == w.Domain)).TotalMilliseconds).ToList();

                WebsiteListView.ItemsSource = websiteDisplayData;

                System.Diagnostics.Debug.WriteLine($"MainWindow: Website list refreshed with {websiteDisplayData.Count} websites");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error refreshing website list: {ex.Message}");
            }
        }

        private void UpdateWebBrowsingStats()
        {
            try
            {
                var today = DateTime.Today;
                var websites = _screenTimeService.GetAllWebsites().ToList();

                // Update period label based on current period
                if (WebBrowsingTimeLabel != null)
                {
                    WebBrowsingTimeLabel.Text = _currentPeriod switch
                    {
                        "Today" => "Today's Browsing Time",
                        "Yesterday" => "Yesterday's Browsing Time", 
                        "This Week" => "This Week's Browsing Time",
                        "Last Week" => "Last Week's Browsing Time",
                        "This Month" => "This Month's Browsing Time",
                        _ => "Total Browsing Time"
                    };
                }

                if (WebBrowsingSwitchesLabel != null)
                {
                    WebBrowsingSwitchesLabel.Text = _currentPeriod switch
                    {
                        "Today" => "Today's Site Switches",
                        "Yesterday" => "Yesterday's Site Switches",
                        "This Week" => "This Week's Site Switches", 
                        "Last Week" => "Last Week's Site Switches",
                        "This Month" => "This Month's Site Switches",
                        _ => "Total Site Switches"
                    };
                }

                // Filter websites based on current period
                var filteredWebsites = GetFilteredWebsites(websites);

                // Total websites count
                if (TotalWebsitesText != null)
                    TotalWebsitesText.Text = filteredWebsites.Count.ToString();

                // Total browsing time
                TimeSpan totalBrowsingTime = TimeSpan.FromMilliseconds(
                    filteredWebsites.Sum(w => GetTimeForCurrentPeriod(w).TotalMilliseconds));

                if (TotalBrowsingTimeText != null)
                {
                    var hours = (int)totalBrowsingTime.TotalHours;
                    var minutes = totalBrowsingTime.Minutes;
                    TotalBrowsingTimeText.Text = $"{hours}h {minutes}m";
                }

                // Total site switches
                int totalSwitches = _currentPeriod switch
                {
                    "Today" => websites.Sum(w => w.TodaysSessionCount),
                    "Yesterday" => websites.Sum(w => w.GetSessionsForDate(today.AddDays(-1))),
                    "This Week" => websites.Sum(w => Enumerable.Range(0, 7)
                        .Sum(i => w.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek + i)))),
                    "Last Week" => websites.Sum(w => Enumerable.Range(0, 7)
                        .Sum(i => w.GetSessionsForDate(today.AddDays(-(int)today.DayOfWeek - 7 + i)))),
                    "This Month" => websites.Sum(w => Enumerable.Range(0, DateTime.DaysInMonth(today.Year, today.Month))
                        .Sum(i => w.GetSessionsForDate(new DateTime(today.Year, today.Month, i + 1)))),
                    _ => websites.Sum(w => w.SessionCount)
                };

                if (TotalBrowsingSwitchesText != null)
                    TotalBrowsingSwitchesText.Text = totalSwitches.ToString();

                // Top website
                var topWebsite = filteredWebsites
                    .OrderByDescending(w => GetTimeForCurrentPeriod(w).TotalMilliseconds)
                    .FirstOrDefault();

                if (TopWebsiteText != null)
                {
                    TopWebsiteText.Text = topWebsite?.DisplayName ?? "None";
                }

                System.Diagnostics.Debug.WriteLine("MainWindow: Web browsing stats updated");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error updating web browsing stats: {ex.Message}");
            }
        }

        private List<WebsiteScreenTime> GetFilteredWebsites(List<WebsiteScreenTime> websites)
        {
            var today = DateTime.Today;

            return _currentPeriod switch
            {
                "Today" => websites.Where(w => w.DailyTimes.ContainsKey(today)).ToList(),
                "Yesterday" => websites.Where(w => w.DailyTimes.ContainsKey(today.AddDays(-1))).ToList(),
                "This Week" => websites.Where(w => w.DailyTimes.Any(dt => 
                    dt.Key >= today.AddDays(-(int)today.DayOfWeek) && dt.Key <= today)).ToList(),
                "Last Week" => websites.Where(w => w.DailyTimes.Any(dt => 
                    dt.Key >= today.AddDays(-(int)today.DayOfWeek - 7) && 
                    dt.Key <= today.AddDays(-(int)today.DayOfWeek - 1))).ToList(),
                "This Month" => websites.Where(w => w.DailyTimes.Any(dt => 
                    dt.Key.Year == today.Year && dt.Key.Month == today.Month)).ToList(),
                _ => websites
            };
        }

        private TimeSpan GetTimeForCurrentPeriod(WebsiteScreenTime website)
        {
            var today = DateTime.Today;

            return _currentPeriod switch
            {
                "Today" => website.GetTimeForDate(today),
                "Yesterday" => website.GetTimeForDate(today.AddDays(-1)),
                "This Week" => website.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek)),
                "Last Week" => website.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek - 7)),
                "This Month" => website.GetMonthTotal(today.Year, today.Month),
                _ => website.TotalTime
            };
        }

        private TimeSpan GetTimeForCurrentPeriod(AppScreenTime app)
        {
            var today = DateTime.Today;

            return _currentPeriod switch
            {
                "Today" => app.TodaysTime,
                "Yesterday" => app.GetTimeForDate(today.AddDays(-1)),
                "This Week" => app.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek)),
                "Last Week" => app.GetWeekTotal(today.AddDays(-(int)today.DayOfWeek - 7)),
                "This Month" => app.GetMonthTotal(today.Year, today.Month),
                _ => app.TotalTime
            };
        }

        private string GetFormattedTimeShort(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours}h {time.Minutes}m";
            }
            else if (time.TotalMinutes >= 1)
            {
                return $"{(int)time.TotalMinutes}m {time.Seconds}s";
            }
            else
            {
                return $"{time.Seconds}s";
            }
        }

        private void HideWebBrowsingPage()
        {
            try
            {
                var webBrowsingContent = WebBrowsingContent;
                var screenTimeContent = ScreenTimeContent;

                if (webBrowsingContent == null || screenTimeContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - WebBrowsingContent or ScreenTimeContent is null");
                    return;
                }

                // Ensure transforms are set up
                if (webBrowsingContent.RenderTransform == null)
                    webBrowsingContent.RenderTransform = new TranslateTransform();
                if (screenTimeContent.RenderTransform == null)
                    screenTimeContent.RenderTransform = new TranslateTransform();

                // Animate the transition
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, webBrowsingContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        webBrowsingContent.Visibility = Visibility.Collapsed;
                        screenTimeContent.Visibility = Visibility.Visible;
                        screenTimeContent.Opacity = 0;

                        // Trigger fade-in animation for main content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, screenTimeContent);
                            fadeInStoryboard.Begin();
                        }

                        // Reset to main period and clear category filter
                        _currentPeriod = "Today";
                        _currentCategoryFilter = null;
                        UpdateLabelsForCurrentContext();
                        RefreshAppList();
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    webBrowsingContent.Visibility = Visibility.Collapsed;
                    screenTimeContent.Visibility = Visibility.Visible;
                    _currentPeriod = "Today";
                    _currentCategoryFilter = null;
                    UpdateLabelsForCurrentContext();
                    RefreshAppList();
                }

                System.Diagnostics.Debug.WriteLine("MainWindow: Web Browsing page hidden, returned to main content");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error hiding web browsing page: {ex.Message}");
                MessageBox.Show($"Error navigating back: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                // Navigation Visibility Settings
                var newShowTodayTab = GetPageCheckBoxValue("PageShowTodayTabCheckBox");
                if (_workingPageSettings.ShowTodayTab != newShowTodayTab)
                {
                    _workingPageSettings.ShowTodayTab = newShowTodayTab;
                    changedSettings.Add($"Today tab is now {(newShowTodayTab ? "visible" : "hidden")}");
                }

                var newShowYesterdayTab = GetPageCheckBoxValue("PageShowYesterdayTabCheckBox");
                if (_workingPageSettings.ShowYesterdayTab != newShowYesterdayTab)
                {
                    _workingPageSettings.ShowYesterdayTab = newShowYesterdayTab;
                    changedSettings.Add($"Yesterday tab is now {(newShowYesterdayTab ? "visible" : "hidden")}");
                }

                var newShowThisWeekTab = GetPageCheckBoxValue("PageShowThisWeekTabCheckBox");
                if (_workingPageSettings.ShowThisWeekTab != newShowThisWeekTab)
                {
                    _workingPageSettings.ShowThisWeekTab = newShowThisWeekTab;
                    changedSettings.Add($"This Week tab is now {(newShowThisWeekTab ? "visible" : "hidden")}");
                }

                var newShowLastWeekTab = GetPageCheckBoxValue("PageShowLastWeekTabCheckBox");
                if (_workingPageSettings.ShowLastWeekTab != newShowLastWeekTab)
                {
                    _workingPageSettings.ShowLastWeekTab = newShowLastWeekTab;
                    changedSettings.Add($"Last Week tab is now {(newShowLastWeekTab ? "visible" : "hidden")}");
                }

                var newShowThisMonthTab = GetPageCheckBoxValue("PageShowThisMonthTabCheckBox");
                if (_workingPageSettings.ShowThisMonthTab != newShowThisMonthTab)
                {
                    _workingPageSettings.ShowThisMonthTab = newShowThisMonthTab;
                    changedSettings.Add($"This Month tab is now {(newShowThisMonthTab ? "visible" : "hidden")}");
                }

                var newShowSleepTab = GetPageCheckBoxValue("PageShowSleepTabCheckBox");
                if (_workingPageSettings.ShowSleepTab != newShowSleepTab)
                {
                    _workingPageSettings.ShowSleepTab = newShowSleepTab;
                    changedSettings.Add($"Sleep tab is now {(newShowSleepTab ? "visible" : "hidden")}");
                }

                var newShowWebBrowsingTab = GetPageCheckBoxValue("PageShowWebBrowsingTabCheckBox");
                if (_workingPageSettings.ShowWebBrowsingTab != newShowWebBrowsingTab)
                {
                    _workingPageSettings.ShowWebBrowsingTab = newShowWebBrowsingTab;
                    changedSettings.Add($"Web Browsing tab is now {(newShowWebBrowsingTab ? "visible" : "hidden")}");
                }

                var newShowDevelopmentTab = GetPageCheckBoxValue("PageShowDevelopmentTabCheckBox");
                if (_workingPageSettings.ShowDevelopmentTab != newShowDevelopmentTab)
                {
                    _workingPageSettings.ShowDevelopmentTab = newShowDevelopmentTab;
                    changedSettings.Add($"Development tab is now {(newShowDevelopmentTab ? "visible" : "hidden")}");
                }

                var newShowGamingTab = GetPageCheckBoxValue("PageShowGamingTabCheckBox");
                if (_workingPageSettings.ShowGamingTab != newShowGamingTab)
                {
                    _workingPageSettings.ShowGamingTab = newShowGamingTab;
                    changedSettings.Add($"Gaming tab is now {(newShowGamingTab ? "visible" : "hidden")}");
                }

                var newShowCommunicationTab = GetPageCheckBoxValue("PageShowCommunicationTabCheckBox");
                if (_workingPageSettings.ShowCommunicationTab != newShowCommunicationTab)
                {
                    _workingPageSettings.ShowCommunicationTab = newShowCommunicationTab;
                    changedSettings.Add($"Communication tab is now {(newShowCommunicationTab ? "visible" : "hidden")}");
                }

                var newShowProductivityTab = GetPageCheckBoxValue("PageShowProductivityTabCheckBox");
                if (_workingPageSettings.ShowProductivityTab != newShowProductivityTab)
                {
                    _workingPageSettings.ShowProductivityTab = newShowProductivityTab;
                    changedSettings.Add($"Productivity tab is now {(newShowProductivityTab ? "visible" : "hidden")}");
                }

                var newShowEntertainmentTab = GetPageCheckBoxValue("PageShowEntertainmentTabCheckBox");
                if (_workingPageSettings.ShowEntertainmentTab != newShowEntertainmentTab)
                {
                    _workingPageSettings.ShowEntertainmentTab = newShowEntertainmentTab;
                    changedSettings.Add($"Entertainment tab is now {(newShowEntertainmentTab ? "visible" : "hidden")}");
                }

                // Chart Filtering Settings
                var newShowUncategorizedInCharts = GetPageCheckBoxValue("PageShowUncategorizedInChartsCheckBox");
                if (_workingPageSettings.ShowUncategorizedInCharts != newShowUncategorizedInCharts)
                {
                    _workingPageSettings.ShowUncategorizedInCharts = newShowUncategorizedInCharts;
                    changedSettings.Add($"Uncategorized items in charts are now {(newShowUncategorizedInCharts ? "visible" : "hidden")}");
                }

                var newShowDevelopmentInCharts = GetPageCheckBoxValue("PageShowDevelopmentInChartsCheckBox");
                if (_workingPageSettings.ShowDevelopmentInCharts != newShowDevelopmentInCharts)
                {
                    _workingPageSettings.ShowDevelopmentInCharts = newShowDevelopmentInCharts;
                    changedSettings.Add($"Development items in charts are now {(newShowDevelopmentInCharts ? "visible" : "hidden")}");
                }

                var newShowGamingInCharts = GetPageCheckBoxValue("PageShowGamingInChartsCheckBox");
                if (_workingPageSettings.ShowGamingInCharts != newShowGamingInCharts)
                {
                    _workingPageSettings.ShowGamingInCharts = newShowGamingInCharts;
                    changedSettings.Add($"Gaming items in charts are now {(newShowGamingInCharts ? "visible" : "hidden")}");
                }

                var newShowCommunicationInCharts = GetPageCheckBoxValue("PageShowCommunicationInChartsCheckBox");
                if (_workingPageSettings.ShowCommunicationInCharts != newShowCommunicationInCharts)
                {
                    _workingPageSettings.ShowCommunicationInCharts = newShowCommunicationInCharts;
                    changedSettings.Add($"Communication items in charts are now {(newShowCommunicationInCharts ? "visible" : "hidden")}");
                }

                var newShowProductivityInCharts = GetPageCheckBoxValue("PageShowProductivityInChartsCheckBox");
                if (_workingPageSettings.ShowProductivityInCharts != newShowProductivityInCharts)
                {
                    _workingPageSettings.ShowProductivityInCharts = newShowProductivityInCharts;
                    changedSettings.Add($"Productivity items in charts are now {(newShowProductivityInCharts ? "visible" : "hidden")}");
                }

                var newShowEntertainmentInCharts = GetPageCheckBoxValue("PageShowEntertainmentInChartsCheckBox");
                if (_workingPageSettings.ShowEntertainmentInCharts != newShowEntertainmentInCharts)
                {
                    _workingPageSettings.ShowEntertainmentInCharts = newShowEntertainmentInCharts;
                    changedSettings.Add($"Entertainment items in charts are now {(newShowEntertainmentInCharts ? "visible" : "hidden")}");
                }

                var newShowCustomCategoriesInCharts = GetPageCheckBoxValue("PageShowCustomCategoriesInChartsCheckBox");
                if (_workingPageSettings.ShowCustomCategoriesInCharts != newShowCustomCategoriesInCharts)
                {
                    _workingPageSettings.ShowCustomCategoriesInCharts = newShowCustomCategoriesInCharts;
                    changedSettings.Add($"Custom categories in charts are now {(newShowCustomCategoriesInCharts ? "visible" : "hidden")}");
                }

                var newEnableChartAnimations = GetPageCheckBoxValue("PageEnableChartAnimationsCheckBox");
                if (_workingPageSettings.EnableChartAnimations != newEnableChartAnimations)
                {
                    _workingPageSettings.EnableChartAnimations = newEnableChartAnimations;
                    changedSettings.Add($"Chart animations are now {(newEnableChartAnimations ? "enabled" : "disabled")}");
                }

                // Power Scheduling Settings (Non-persistent)
                var newEnablePowerScheduling = GetPageCheckBoxValue("PageEnablePowerSchedulingCheckBox");
                if (_workingPageSettings.EnablePowerScheduling != newEnablePowerScheduling)
                {
                    _workingPageSettings.EnablePowerScheduling = newEnablePowerScheduling;
                    changedSettings.Add($"Power scheduling is now {(newEnablePowerScheduling ? "enabled" : "disabled")}");
                }

                var newPowerScheduleHours = GetPageIntTextBoxValue("PagePowerScheduleHoursTextBox", 0);
                if (_workingPageSettings.PowerScheduleHours != newPowerScheduleHours)
                {
                    _workingPageSettings.PowerScheduleHours = newPowerScheduleHours;
                    changedSettings.Add($"Power schedule default hours changed to {newPowerScheduleHours}");
                }

                var newPowerScheduleMinutes = GetPageIntTextBoxValue("PagePowerScheduleMinutesTextBox", 30);
                if (_workingPageSettings.PowerScheduleMinutes != newPowerScheduleMinutes)
                {
                    _workingPageSettings.PowerScheduleMinutes = newPowerScheduleMinutes;
                    changedSettings.Add($"Power schedule default minutes changed to {newPowerScheduleMinutes}");
                }

                if (PagePowerScheduleActionComboBox != null && PagePowerScheduleActionComboBox.SelectedItem is ComboBoxItem selectedAction && selectedAction.Tag is string actionTag)
                {
                    if (_workingPageSettings.PowerScheduleAction != actionTag)
                    {
                        _workingPageSettings.PowerScheduleAction = actionTag;
                        changedSettings.Add($"Power schedule default action changed to {selectedAction.Content}");
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
                ShowSaveConfirmation("? Error saving settings");
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

        private static int GetIso8601WeekOfYear(DateTime date)
        {
            var thursday = date.AddDays(3 - ((int)date.DayOfWeek + 6) % 7);
            return (thursday.DayOfYear - 1) / 7 + 1;
        }

        private void ShowDebugWindow_Click(object sender, RoutedEventArgs e)
        {
            Windows.DebugConsoleWindow.Instance.Show();
            Windows.DebugConsoleWindow.Instance.Activate();
        }

        #region Sleep Methods

        private void RefreshSleepData()
        {
            try
            {
                if (!_sleepService.IsSleepDataAvailable())
                {
                    // Show message about missing sleep data
                    if (SleepDataList != null)
                        SleepDataList.ItemsSource = new List<DailySleepSummary>();
                    
                    UpdateSleepStats(new List<DailySleepSummary>());
                    BuildSleepNavigationTree(new List<DailySleepSummary>());
                    UpdateCurrentSelection("All Sleep Data", "No sleep data available", 0);
                    System.Diagnostics.Debug.WriteLine($"Sleep data file not found at: {_sleepService.GetSleepDataPath()}");
                    return;
                }

                var dailySummaries = _sleepService.GetDailySummaries();
                
                // Build navigation tree
                BuildSleepNavigationTree(dailySummaries);
                
                // Show all data by default
                if (SleepDataList != null)
                    SleepDataList.ItemsSource = dailySummaries;
                
                UpdateSleepStats(dailySummaries);
                System.Diagnostics.Debug.WriteLine($"Sleep data refreshed: {dailySummaries.Count} days loaded");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing sleep data: {ex.Message}");
                MessageBox.Show($"Error loading sleep data: {ex.Message}", "Sleep Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BuildSleepNavigationTree(List<DailySleepSummary> summaries)
        {
            try
            {
                if (SleepNavigationTree == null) return;
                
                SleepNavigationTree.Items.Clear();
                
                if (!summaries.Any())
                {
                    // Add "No Data" node
                    var noDataNode = new Models.SleepNavigationNode
                    {
                        DisplayName = "No sleep data available",
                        NodeType = Models.SleepNavigationNodeType.AllData,
                        Data = new List<DailySleepSummary>()
                    };
                    var noDataItem = new TreeViewItem { Header = noDataNode.DisplayName, Tag = noDataNode };
                    SleepNavigationTree.Items.Add(noDataItem);
                    
                    // Show all data when no specific selection
                    UpdateCurrentSelection("All Sleep Data", "No sleep data available", 0);
                    return;
                }
                
                // Group by year
                var yearGroups = summaries
                    .GroupBy(s => s.Date.Year)
                    .OrderByDescending(g => g.Key);
                
                foreach (var yearGroup in yearGroups)
                {
                    var yearData = new Models.SleepYearData
                    {
                        Year = yearGroup.Key,
                        Sessions = yearGroup.ToList()
                    };
                    
                    var avgHoursPerDay = yearData.TotalSleep.TotalHours / Math.Max(1, yearData.Sessions.Count);
                    var yearNode = new Models.SleepNavigationNode
                    {
                        DisplayName = $"{yearGroup.Key} ({avgHoursPerDay:F1}h avg)",
                        NodeType = Models.SleepNavigationNodeType.Year,
                        Data = yearData,
                        IsExpanded = yearGroup.Key == DateTime.Now.Year // Expand current year
                    };
                    
                    var yearItem = new TreeViewItem 
                    { 
                        Header = yearNode.DisplayName, 
                        Tag = yearNode,
                        IsExpanded = true // Always expand all years
                    };
                    
                    // Group by month within year
                    var monthGroups = yearGroup
                        .GroupBy(s => s.Date.Month)
                        .OrderByDescending(g => g.Key);
                    
                    foreach (var monthGroup in monthGroups)
                    {
                        var monthData = new Models.SleepMonthData
                        {
                            Year = yearGroup.Key,
                            Month = monthGroup.Key,
                            Sessions = monthGroup.ToList()
                        };
                        
                        var monthAvgHoursPerDay = monthData.TotalSleep.TotalHours / Math.Max(1, monthData.Sessions.Count);
                        var monthNode = new Models.SleepNavigationNode
                        {
                            DisplayName = $"{monthData.MonthName} ({monthAvgHoursPerDay:F1}h avg)",
                            NodeType = Models.SleepNavigationNodeType.Month,
                            Data = monthData
                        };
                        
                        var monthItem = new TreeViewItem { Header = monthNode.DisplayName, Tag = monthNode };
                        yearItem.Items.Add(monthItem);
                    }
                    
                    SleepNavigationTree.Items.Add(yearItem);
                }
                
                // Auto-select current month if it exists, otherwise show all data
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;
                var foundCurrentMonth = false;
                
                foreach (TreeViewItem yearItem in SleepNavigationTree.Items)
                {
                    if (yearItem.Tag is Models.SleepNavigationNode yearNode && 
                        yearNode.Data is Models.SleepYearData year && year.Year == currentYear)
                    {
                        foreach (TreeViewItem monthItem in yearItem.Items)
                        {
                            if (monthItem.Tag is Models.SleepNavigationNode monthNode &&
                                monthNode.Data is Models.SleepMonthData month && month.Month == currentMonth)
                            {
                                monthItem.IsSelected = true;
                                SelectNavigationNode(monthNode);
                                foundCurrentMonth = true;
                                break;
                            }
                        }
                        if (foundCurrentMonth) break;
                    }
                }
                
                // If no current month found, show all data
                if (!foundCurrentMonth)
                {
                    UpdateCurrentSelection("All Sleep Data", "Showing all available sleep sessions", summaries.Sum(s => s.SessionCount));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building sleep navigation tree: {ex.Message}");
            }
        }
        
        private void SleepNavigationTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is Models.SleepNavigationNode node)
            {
                SelectNavigationNode(node);
            }
        }
        
        private void SelectNavigationNode(Models.SleepNavigationNode node)
        {
            try
            {
                List<DailySleepSummary> sessionsToShow = new();
                string title = "Sleep Data";
                string subtitle = "No sessions selected";
                
                switch (node.NodeType)
                {
                    case Models.SleepNavigationNodeType.AllData:
                        if (node.Data is List<DailySleepSummary> allSessions)
                        {
                            sessionsToShow = allSessions;
                            title = "All Sleep Data";
                            subtitle = "Showing all available sleep sessions";
                        }
                        break;
                        
                    case Models.SleepNavigationNodeType.Year:
                        if (node.Data is Models.SleepYearData yearData)
                        {
                            sessionsToShow = yearData.Sessions;
                            title = $"Sleep Data - {yearData.Year}";
                            subtitle = $"Showing all sessions from {yearData.Year}";
                        }
                        break;
                        
                    case Models.SleepNavigationNodeType.Month:
                        if (node.Data is Models.SleepMonthData monthData)
                        {
                            sessionsToShow = monthData.Sessions;
                            title = $"Sleep Data - {monthData.MonthName} {monthData.Year}";
                            subtitle = $"Showing sessions from {monthData.MonthName} {monthData.Year}";
                        }
                        break;
                }
                
                // Update the display
                if (SleepDataList != null)
                    SleepDataList.ItemsSource = sessionsToShow;
                
                UpdateCurrentSelection(title, subtitle, sessionsToShow.Sum(s => s.SessionCount));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting navigation node: {ex.Message}");
            }
        }
        
        private void UpdateCurrentSelection(string title, string subtitle, int sessionCount)
        {
            try
            {
                if (CurrentSelectionTitle != null)
                    CurrentSelectionTitle.Text = title;
                    
                if (CurrentSelectionSubtitle != null)
                    CurrentSelectionSubtitle.Text = subtitle;
                    
                if (CurrentSelectionCount != null)
                    CurrentSelectionCount.Text = sessionCount == 1 ? "1 session" : $"{sessionCount} sessions";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating current selection: {ex.Message}");
            }
        }

        private void UpdateSleepStats(List<DailySleepSummary> summaries)
        {
            try
            {
                if (summaries == null || !summaries.Any())
                {
                    if (TotalSleepDaysText != null) TotalSleepDaysText.Text = "0";
                    if (TotalSleepSessionsText != null) TotalSleepSessionsText.Text = "0";
                    if (AverageSleepText != null) AverageSleepText.Text = "0h 0m";
                    if (LastSleepText != null) LastSleepText.Text = "None";
                    return;
                }

                // Total days
                if (TotalSleepDaysText != null)
                    TotalSleepDaysText.Text = summaries.Count.ToString();

                // Total sessions
                var totalSessions = summaries.Sum(s => s.SessionCount);
                if (TotalSleepSessionsText != null)
                    TotalSleepSessionsText.Text = totalSessions.ToString();

                // Average sleep per day
                var totalMinutes = summaries.Sum(s => s.TotalSleepTime.TotalMinutes);
                var averageMinutes = totalMinutes / summaries.Count;
                var averageTime = TimeSpan.FromMinutes(averageMinutes);
                if (AverageSleepText != null)
                {
                    AverageSleepText.Text = averageTime.TotalHours >= 1 
                        ? $"{(int)averageTime.TotalHours}h {averageTime.Minutes}m"
                        : $"{averageTime.Minutes}m";
                }

                // Last sleep session
                var lastSummary = summaries.OrderByDescending(s => s.Date).FirstOrDefault();
                if (LastSleepText != null && lastSummary != null)
                {
                    var lastSession = lastSummary.Sessions.OrderByDescending(s => s.Started).FirstOrDefault();
                    if (lastSession != null)
                    {
                        LastSleepText.Text = $"{lastSession.FormattedDuration} ({lastSession.Started:MM/dd HH:mm})";
                    }
                    else
                    {
                        LastSleepText.Text = "None";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating sleep stats: {ex.Message}");
            }
        }

        private void SleepDayCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.Tag is DailySleepSummary summary)
                {
                    var sleepDetailsWindow = new Windows.SleepDetailsWindow(summary)
                    {
                        Owner = this
                    };
                    sleepDetailsWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing sleep details: {ex.Message}");
                MessageBox.Show($"Failed to show sleep details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HideSleepPage()
        {
            try
            {
                if (SleepContent == null || ScreenTimeContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - SleepContent or ScreenTimeContent is null");
                    return;
                }

                // Animate the transition back to main
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, SleepContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        SleepContent.Visibility = Visibility.Collapsed;
                        ScreenTimeContent.Visibility = Visibility.Visible;
                        ScreenTimeContent.Opacity = 0;

                        // Trigger fade-in animation for main content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, ScreenTimeContent);
                            fadeInStoryboard.Begin();
                        }

                        // Refresh main data (preserves current period and category filter)
                        RefreshAppList();
                        UpdateStatusUI();
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    SleepContent.Visibility = Visibility.Collapsed;
                    ScreenTimeContent.Visibility = Visibility.Visible;
                    RefreshAppList();
                    UpdateStatusUI();
                }

                System.Diagnostics.Debug.WriteLine("MainWindow: Sleep page hidden successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error hiding sleep page: {ex.Message}");
                MessageBox.Show($"Error hiding sleep page: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HideChartsPage()
        {
            try
            {
                if (ChartsContent == null || ScreenTimeContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: Warning - ChartsContent or ScreenTimeContent is null");
                    return;
                }

                // Animate the transition back to main
                var fadeOutStoryboard = this.FindResource("FadeOutDownAnimation") as Storyboard;
                if (fadeOutStoryboard != null)
                {
                    Storyboard.SetTarget(fadeOutStoryboard, ChartsContent);
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        ChartsContent.Visibility = Visibility.Collapsed;
                        ScreenTimeContent.Visibility = Visibility.Visible;
                        ScreenTimeContent.Opacity = 0;

                        // Trigger fade-in animation for main content
                        var fadeInStoryboard = this.FindResource("FadeInUpAnimation") as Storyboard;
                        if (fadeInStoryboard != null)
                        {
                            Storyboard.SetTarget(fadeInStoryboard, ScreenTimeContent);
                            fadeInStoryboard.Begin();
                        }

                        // Refresh main data (preserves current period and category filter)
                        RefreshAppList();
                        UpdateStatusUI();
                    };
                    fadeOutStoryboard.Begin();
                }
                else
                {
                    // Fallback without animation
                    ChartsContent.Visibility = Visibility.Collapsed;
                    ScreenTimeContent.Visibility = Visibility.Visible;
                    RefreshAppList();
                    UpdateStatusUI();
                }

                System.Diagnostics.Debug.WriteLine("MainWindow: Charts page hidden successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error hiding charts page: {ex.Message}");
                MessageBox.Show($"Error hiding charts page: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Services.UpdateService.CheckForUpdatesManuallyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OpenPowerSchedulingDialog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Windows.PowerSchedulingDialog(_powerSchedulingService);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error showing power scheduling dialog from preferences: {ex.Message}");
                await ShowErrorDialogAsync("Error", $"Failed to open power scheduling dialog: {ex.Message}");
            }
        }

        public async Task ShowDownloadStartedDialogAsync(string version, string downloadUrl)
        {
            try
            {
                // Create a popup layer Grid that overlays the entire window
                var popupLayerGrid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
                };

                // Create content presenter for the dialog and add it to the popup layer
                var contentPresenter = new System.Windows.Controls.ContentPresenter();
                popupLayerGrid.Children.Add(contentPresenter);

                // Create a popup window that covers the main window
                var popup = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Owner = this,
                    Content = popupLayerGrid,
                    Width = this.ActualWidth,
                    Height = this.ActualHeight,
                    Left = this.Left,
                    Top = this.Top,
                    WindowState = this.WindowState,
                    Topmost = true
                };

                var hyperlink = new Hyperlink(new Run(downloadUrl))
                {
                    NavigateUri = new Uri(downloadUrl),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212))
                };
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;

                var dialog = new Wpf.Ui.Controls.ContentDialog
                {
                    Title = "Download Started",
                    CloseButtonText = "OK",
                    DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Close,
                    DialogHeight = 250,
                    DialogWidth = 500,
                    DialogMaxWidth = 500,
                    DialogMaxHeight = 250,
                    DialogMargin = new Thickness(16),
                    Content = new System.Windows.Controls.TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14,
                        Margin = new Thickness(0, 10, 0, 10),
                        Inlines =
                        {
                            new Run($"The download page for version {version} has been opened in your browser.\n\n"),
                            new Run("If it did not open, click this link: "),
                            hyperlink,
                            new Run("\n\nPlease download and install the update manually. After installation, restart the application.")
                        }
                    },
                    DialogHost = contentPresenter
                };

                // Handle window state changes
                this.LocationChanged += (s, e) =>
                {
                    popup.Left = this.Left;
                    popup.Top = this.Top;
                };

                this.SizeChanged += (s, e) =>
                {
                    popup.Width = this.ActualWidth;
                    popup.Height = this.ActualHeight;
                    popup.WindowState = this.WindowState;
                };

                // Show the popup
                popup.Show();

                try
                {
                    await dialog.ShowAsync();
                }
                finally
                {
                    // Clean up
                    this.LocationChanged -= (s, e) =>
                    {
                        popup.Left = this.Left;
                        popup.Top = this.Top;
                    };

                    this.SizeChanged -= (s, e) =>
                    {
                        popup.Width = this.ActualWidth;
                        popup.Height = this.ActualHeight;
                        popup.WindowState = this.WindowState;
                    };

                    popup.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing download started dialog: {ex.Message}");
                MessageBox.Show($"Error showing download started dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AppItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AppScreenTime app)
            {
                _lastRightClickedApp = app;
            }
        }

        private void MoveAppToCategory(string? category)
        {
            if (_lastRightClickedApp == null || string.IsNullOrEmpty(category))
                return;
            _screenTimeService.UpdateAppCategory(_lastRightClickedApp.AppName, category);
            RefreshAppList();
            ShowTrayNotification("Category Updated", $"Moved '{_lastRightClickedApp.AppName}' to '{category}'");
        }

        #region Supabase Settings Handlers

        private void ToggleSupabaseKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PageSupabaseAnonKeyPasswordBox == null || PageSupabaseAnonKeyTextBox == null || ToggleSupabaseKeyVisibilityButton == null)
                    return;

                if (PageSupabaseAnonKeyPasswordBox.Visibility == Visibility.Visible)
                {
                    // Show the text box and hide password box
                    PageSupabaseAnonKeyTextBox.Text = PageSupabaseAnonKeyPasswordBox.Password;
                    PageSupabaseAnonKeyTextBox.Visibility = Visibility.Visible;
                    PageSupabaseAnonKeyPasswordBox.Visibility = Visibility.Collapsed;
                    ToggleSupabaseKeyVisibilityButton.Content = "🙈 Hide Key";
                }
                else
                {
                    // Show the password box and hide text box
                    PageSupabaseAnonKeyPasswordBox.Password = PageSupabaseAnonKeyTextBox.Text;
                    PageSupabaseAnonKeyPasswordBox.Visibility = Visibility.Visible;
                    PageSupabaseAnonKeyTextBox.Visibility = Visibility.Collapsed;
                    ToggleSupabaseKeyVisibilityButton.Content = "👁️ Show Key";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling Supabase key visibility: {ex.Message}");
            }
        }

        private void PageSupabaseAnonKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sync password box with text box when password changes
                if (PageSupabaseAnonKeyPasswordBox != null && PageSupabaseAnonKeyTextBox != null)
                {
                    if (PageSupabaseAnonKeyTextBox.Visibility == Visibility.Visible)
                    {
                        PageSupabaseAnonKeyTextBox.Text = PageSupabaseAnonKeyPasswordBox.Password;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing Supabase key: {ex.Message}");
            }
        }

        private async void TestSupabaseConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PageSupabaseUrlTextBox == null || PageSupabaseAnonKeyPasswordBox == null || 
                    PageSupabaseAnonKeyTextBox == null || PageSupabaseUserIdTextBox == null)
                {
                    MessageBox.Show("Some Supabase settings fields are missing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var url = PageSupabaseUrlTextBox.Text?.Trim() ?? string.Empty;
                var anonKey = PageSupabaseAnonKeyPasswordBox.Visibility == Visibility.Visible
                    ? PageSupabaseAnonKeyPasswordBox.Password
                    : PageSupabaseAnonKeyTextBox.Text?.Trim() ?? string.Empty;
                var userId = PageSupabaseUserIdTextBox.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show("Please enter a Supabase URL.", "Missing URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(anonKey))
                {
                    MessageBox.Show("Please enter a Supabase Anon Key.", "Missing Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(userId))
                {
                    MessageBox.Show("Please enter a User ID (UUID).", "Missing User ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate UUID format
                if (!System.Guid.TryParse(userId, out _))
                {
                    MessageBox.Show("User ID must be a valid UUID format.", "Invalid User ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Test connection by creating a temporary upload service and attempting a minimal upload
                var testService = new SupabaseUploadService(url, anonKey, userId, Environment.MachineName);
                
                // Create minimal test data
                var testData = new ScreenTimeData();
                var today = DateTime.Today;
                var year = today.Year;
                var month = today.Month;
                var week = GetIso8601WeekOfYear(today);
                
                if (!testData.Years.ContainsKey(year))
                    testData.Years[year] = new YearData { Year = year };
                if (!testData.Years[year].Months.ContainsKey(month))
                    testData.Years[year].Months[month] = new MonthData { Month = month };
                if (!testData.Years[year].Months[month].Weeks.ContainsKey(week))
                    testData.Years[year].Months[month].Weeks[week] = new WeekData { WeekNumber = week };
                if (!testData.Years[year].Months[month].Weeks[week].Days.ContainsKey(today))
                {
                    testData.Years[year].Months[month].Weeks[week].Days[today] = new DayData { Date = today };
                }

                MessageBox.Show("Testing connection...", "Testing", MessageBoxButton.OK, MessageBoxImage.Information);
                
                var result = await testService.UploadScreentimeDataAsync(testData, userId, Environment.MachineName);
                testService.Dispose();

                if (result.Success)
                {
                    MessageBox.Show(
                        $"Connection successful!\n\nUploaded: {result.AppsInserted} apps, {result.WebsitesInserted} websites",
                        "Connection Test Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"Connection test failed:\n\n{result.ErrorMessage}",
                        "Connection Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error testing connection:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                System.Diagnostics.Debug.WriteLine($"Error testing Supabase connection: {ex.Message}");
            }
        }

        #endregion

        #region Chart Methods

        private void RefreshChartData()
        {
            try
            {
                // Get current chart configuration
                var timePeriod = GetSelectedTimePeriod();
                var chartType = GetSelectedChartType();
                var dataType = GetSelectedDataType();

                // Get chart configuration
                var config = _chartService.GetChartConfiguration(timePeriod, chartType, dataType);

                // Get chart data with current settings
                var currentSettings = _settingsService?.CurrentSettings ?? new AppSettings();
                var dataPoints = _chartService.GetChartData(config, currentSettings);

                // Update chart title and subtitle
                ChartTitleText.Text = _chartService.GetChartTitle(config);
                ChartSubtitleText.Text = _chartService.GetChartSubtitle(config);

                // Update summary stats
                ChartTotalTimeText.Text = _chartService.GetFormattedTotalTime(dataPoints);
                ChartTopCategoryText.Text = _chartService.GetTopCategory(dataPoints);
                ChartCategoriesCountText.Text = _chartService.GetCategoriesCount(dataPoints).ToString();

                // Update legend
                var legendItems = _chartService.GetChartLegend(dataPoints);
                ChartLegendList.ItemsSource = legendItems;

                // Render chart
                RenderChart(chartType, dataPoints);

                System.Diagnostics.Debug.WriteLine($"MainWindow: Chart data refreshed - {dataPoints.Count} data points");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error refreshing chart data: {ex.Message}");
                MessageBox.Show($"Error refreshing chart data: {ex.Message}", "Chart Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenderChart(string chartType, ObservableCollection<ChartDataPoint> dataPoints)
        {
            try
            {
                if (ChartCanvas == null)
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow: ChartCanvas is null");
                    return;
                }

                // Get current settings for animations
                var currentSettings = _settingsService?.CurrentSettings ?? new AppSettings();
                var enableAnimations = currentSettings.EnableChartAnimations;

                // Wait for canvas to be properly sized
                ChartCanvas.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        switch (chartType)
                        {
                            case "Bar":
                                _chartRendererService.RenderBarChart(ChartCanvas, dataPoints, enableAnimations);
                                break;
                            case "Line":
                                _chartRendererService.RenderLineChart(ChartCanvas, dataPoints);
                                break;
                            default:
                                // Optionally do nothing or show an error
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MainWindow: Error rendering chart: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error in RenderChart: {ex.Message}");
            }
        }

        private string GetSelectedTimePeriod()
        {
            if (ChartTimePeriodComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Tag?.ToString() ?? "Today";
            }
            return "Today";
        }

        private string GetSelectedChartType()
        {
            if (ChartTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Tag?.ToString() ?? "Bar";
            }
            return "Bar";
        }

        private string GetSelectedDataType()
        {
            if (ChartDataTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Tag?.ToString() ?? "Category";
            }
            return "Category";
        }

        private void ChartTimePeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChartsContent?.Visibility == Visibility.Visible)
            {
                RefreshChartData();
            }
        }

        private void ChartDataType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChartsContent?.Visibility == Visibility.Visible)
            {
                RefreshChartData();
            }
        }

        private void ChartType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChartsContent?.Visibility == Visibility.Visible)
            {
                RefreshChartData();
            }
        }



        #endregion

        // RelayCommand implementation (if not already present)
        public class RelayCommand<T> : ICommand
        {
            private readonly Action<T?> _execute;
            private readonly Predicate<T?>? _canExecute;
            public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }
            public bool CanExecute(object? parameter) => _canExecute == null || _canExecute((T?)parameter);
            public void Execute(object? parameter) => _execute((T?)parameter);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }

        private void UpdateCustomCategoryTogglesPanel(AppSettings settings)
        {
            var categoryService = _screenTimeService.GetCategoryService();
            var customCategories = categoryService.GetCustomCategories();
            CustomCategoryTogglesPanel.Items.Clear();
            foreach (var category in customCategories)
            {
                var toggle = new Wpf.Ui.Controls.ToggleSwitch
                {
                    Content = category,
                    IsChecked = settings.ShowCustomCategoryTabs.TryGetValue(category, out var show) ? show : true,
                    Margin = new Thickness(0, 0, 0, 8),
                    Tag = category
                };
                CustomCategoryTogglesPanel.Items.Add(toggle);
            }
        }
    }
}
