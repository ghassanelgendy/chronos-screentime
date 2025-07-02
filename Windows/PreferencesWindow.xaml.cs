using chronos_screentime.Models;
using chronos_screentime.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace chronos_screentime.Windows
{
    public partial class PreferencesWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly SettingsService _settingsService;
        private AppSettings _workingSettings;

        public PreferencesWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _workingSettings = _settingsService.CurrentSettings.Clone();

            PopulateNotificationSoundComboBox();
            LoadSettingsToUI();
        }

        private void PopulateNotificationSoundComboBox()
        {
            if (FindName("NotificationSoundComboBox") is ComboBox comboBox)
            {
                string wavDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "wav");
                if (Directory.Exists(wavDir))
                {
                    var files = Directory.GetFiles(wavDir, "*.wav").Select(Path.GetFileName).ToList();
                    comboBox.ItemsSource = files;
                    // Select current setting or default
                    comboBox.SelectedItem = _workingSettings.NotificationSoundFile ?? files.FirstOrDefault();
                }
            }
        }

        private void LoadSettingsToUI()
        {
            try
            {
                // General Settings
                SetCheckBoxValue("AlwaysOnTopCheckBox", _workingSettings.AlwaysOnTop);
                SetCheckBoxValue("ShowInSystemTrayCheckBox", _workingSettings.ShowInSystemTray);
                SetCheckBoxValue("HideTitleBarCheckBox", _workingSettings.HideTitleBar);

                // Break Notifications
                SetCheckBoxValue("EnableBreakNotificationsCheckBox", _workingSettings.EnableBreakNotifications);
                SetTextBoxValue("BreakReminderMinutesTextBox", _workingSettings.BreakReminderMinutes.ToString());

                // Screen Break Notifications
                SetCheckBoxValue("EnableScreenBreakNotificationsCheckBox", _workingSettings.EnableScreenBreakNotifications);
                SetTextBoxValue("ScreenBreakReminderMinutesTextBox", _workingSettings.ScreenBreakReminderMinutes.ToString());
                SetTextBoxValue("ScreenBreakDurationSecondsTextBox", _workingSettings.ScreenBreakDurationSeconds.ToString());
                SetCheckBoxValue("ShowFullScreenBreakOverlayCheckBox", _workingSettings.ShowFullScreenBreakOverlay);
                SetCheckBoxValue("DimScreenDuringBreakCheckBox", _workingSettings.DimScreenDuringBreak);
                SetCheckBoxValue("PlaySoundWithBreakReminderCheckBox", _workingSettings.PlaySoundWithBreakReminder);

                // Theme selection
                SetThemeComboBoxValue(_workingSettings.Theme);

                System.Diagnostics.Debug.WriteLine($"Settings loaded to UI - ShowInTray: {_workingSettings.ShowInSystemTray}, AlwaysOnTop: {_workingSettings.AlwaysOnTop}, Theme: {_workingSettings.Theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings to UI: {ex.Message}");
            }
        }

        private void SetCheckBoxValue(string name, bool value)
        {
            if (FindName(name) is CheckBox checkBox)
            {
                checkBox.IsChecked = value;
            }
        }

        private void SetTextBoxValue(string name, string value)
        {
            if (FindName(name) is TextBox textBox)
            {
                textBox.Text = value;
            }
        }

        private void SaveUIToSettings()
        {
            try
            {
                // Store previous values for debugging
                var oldShowInTray = _workingSettings.ShowInSystemTray;
                var oldTheme = _workingSettings.Theme;
                var oldAlwaysOnTop = _workingSettings.AlwaysOnTop;

                // General Settings
                _workingSettings.AlwaysOnTop = GetCheckBoxValue("AlwaysOnTopCheckBox");
                _workingSettings.ShowInSystemTray = GetCheckBoxValue("ShowInSystemTrayCheckBox");
                _workingSettings.HideTitleBar = GetCheckBoxValue("HideTitleBarCheckBox");

                // Break Notifications
                _workingSettings.EnableBreakNotifications = GetCheckBoxValue("EnableBreakNotificationsCheckBox");
                _workingSettings.BreakReminderMinutes = GetIntTextBoxValue("BreakReminderMinutesTextBox", 30);

                // Screen Break Notifications
                _workingSettings.EnableScreenBreakNotifications = GetCheckBoxValue("EnableScreenBreakNotificationsCheckBox");
                _workingSettings.ScreenBreakReminderMinutes = GetIntTextBoxValue("ScreenBreakReminderMinutesTextBox", 20);
                _workingSettings.ScreenBreakDurationSeconds = GetIntTextBoxValue("ScreenBreakDurationSecondsTextBox", 20);
                _workingSettings.ShowFullScreenBreakOverlay = GetCheckBoxValue("ShowFullScreenBreakOverlayCheckBox");
                _workingSettings.DimScreenDuringBreak = GetCheckBoxValue("DimScreenDuringBreakCheckBox");
                _workingSettings.PlaySoundWithBreakReminder = GetCheckBoxValue("PlaySoundWithBreakReminderCheckBox");

                // Notification sound selection
                if (FindName("NotificationSoundComboBox") is ComboBox comboBox && comboBox.SelectedItem is string selectedSound)
                {
                    _workingSettings.NotificationSoundFile = selectedSound;
                }

                // Theme selection
                _workingSettings.Theme = GetThemeComboBoxValue();

                System.Diagnostics.Debug.WriteLine($"Settings saved from UI - ShowInTray: {oldShowInTray} → {_workingSettings.ShowInSystemTray}, AlwaysOnTop: {oldAlwaysOnTop} → {_workingSettings.AlwaysOnTop}, Theme: {oldTheme} → {_workingSettings.Theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings from UI: {ex.Message}");
            }
        }

        private bool GetCheckBoxValue(string name)
        {
            if (FindName(name) is CheckBox checkBox)
            {
                return checkBox.IsChecked == true;
            }
            return false;
        }

        private int GetIntTextBoxValue(string name, int defaultValue)
        {
            if (FindName(name) is TextBox textBox)
            {
                if (int.TryParse(textBox.Text, out int value) && value > 0)
                {
                    return value;
                }
            }
            return defaultValue;
        }

        private void SetThemeComboBoxValue(string theme)
        {
            if (FindName("ThemeComboBox") is ComboBox themeComboBox)
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

        private string GetThemeComboBoxValue()
        {
            if (FindName("ThemeComboBox") is ComboBox themeComboBox &&
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

        private void ApplyThemeChange(string theme)
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

                // Apply theme to the application globally first
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(themeToApply);

                // Apply theme to this window
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

                // Force a complete UI refresh for this window
                this.InvalidateVisual();
                this.UpdateLayout();

                // Refresh all child controls in this window
                RefreshControlThemes(this);

                // Update the main window if it's open
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(mainWindow);
                    mainWindow.RefreshTheme();
                }

                System.Diagnostics.Debug.WriteLine($"Theme applied: {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme: {ex.Message}");
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
                        element.InvalidateVisual();
                        element.UpdateLayout();

                        // Special handling for WPF.UI controls
                        if (child is Wpf.Ui.Controls.Button button)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(button);
                        }
                        else if (child is TabControl tabControl)
                        {
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(tabControl);
                        }
                    }

                    // Recursively process children
                    RefreshControlThemes(child);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing control themes in preferences: {ex.Message}");
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string newTheme = selectedItem.Tag?.ToString() switch
                {
                    "Dark" => "Dark Theme",
                    "Auto" => "Auto (System)",
                    _ => "Light Theme"
                };

                // Update working settings
                _workingSettings.Theme = newTheme;

                // Apply theme immediately for preview
                ApplyThemeChange(newTheme);
            }
        }

        private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = await ShowConfirmationDialogAsync(
                "Reset to Defaults",
                "Are you sure you want to reset all preferences to default values?");

            if (confirmed)
            {
                _workingSettings = new AppSettings();
                LoadSettingsToUI();
                await ShowInfoDialogAsync("Reset Complete", "Preferences have been reset to defaults.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveUIToSettings();
                _settingsService.SaveSettings(_workingSettings);
                await ShowInfoDialogAsync("Apply", "Preferences applied successfully.");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Error", $"Error applying preferences: {ex.Message}");
            }
        }

        private async void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveUIToSettings();
                _settingsService.SaveSettings(_workingSettings);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Error", $"Error saving preferences: {ex.Message}");
            }
        }

        private async Task<Wpf.Ui.Controls.ContentDialogResult> ShowContentDialogAsync(
            string title,
            string content,
            string primaryButtonText = "OK",
            string? secondaryButtonText = null,
            string? closeButtonText = null)
        {
            var dialog = new Wpf.Ui.Controls.ContentDialog
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                CloseButtonText = closeButtonText
            };

            return await dialog.ShowAsync();
        }

        private async Task ShowInfoDialogAsync(string title, string message)
        {
            await ShowContentDialogAsync(title, message);
        }

        private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            var result = await ShowContentDialogAsync(
                title,
                message,
                "Yes",
                "No");

            return result == Wpf.Ui.Controls.ContentDialogResult.Primary;
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            await ShowContentDialogAsync(title, message, "OK");
        }
    }
}