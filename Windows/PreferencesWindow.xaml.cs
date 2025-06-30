using System;
using System.Windows;
using System.Windows.Controls;
using chronos_screentime.Models;
using chronos_screentime.Services;

namespace chronos_screentime.Windows
{
    public partial class PreferencesWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _workingSettings;

        public PreferencesWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _workingSettings = _settingsService.CurrentSettings.Clone();
            
            LoadSettingsToUI();
        }

        private void LoadSettingsToUI()
        {
            try
            {
                // Find controls by name and set their values
                // This is a simplified approach - in a real app you might use data binding
                
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
                
                System.Diagnostics.Debug.WriteLine("Settings loaded to UI");
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
                
                System.Diagnostics.Debug.WriteLine("Settings saved from UI");
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

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = ThemedMessageBox.Show(
                this,
                "Are you sure you want to reset all preferences to default values?",
                "Reset to Defaults",
                ThemedMessageBox.MessageButtons.YesNo,
                ThemedMessageBox.MessageType.Question);

            if (result == MessageBoxResult.Yes)
            {
                _workingSettings = new AppSettings();
                LoadSettingsToUI();
                ThemedMessageBox.Show(this, "Preferences have been reset to defaults.", "Reset Complete", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveUIToSettings();
                _settingsService.SaveSettings(_workingSettings);
                ThemedMessageBox.Show(this, "Preferences applied successfully.", "Apply", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(this, $"Error applying preferences: {ex.Message}", "Error", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Error);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
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
                ThemedMessageBox.Show(this, $"Error saving preferences: {ex.Message}", "Error", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Error);
            }
        }
    }
} 