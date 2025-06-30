using System;
using System.IO;
using chronos_screentime.Models;
using Newtonsoft.Json;

namespace chronos_screentime.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private AppSettings _currentSettings;

        public event EventHandler<AppSettings>? SettingsChanged;

        public AppSettings CurrentSettings => _currentSettings;

        public SettingsService()
        {
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChronosScreenTime",
                "settings.json"
            );
            
            _currentSettings = LoadSettings();
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        // Migrate existing settings to ensure new defaults are applied
                        var migratedSettings = MigrateSettings(settings);
                        return migratedSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            // Return default settings if loading fails
            return new AppSettings();
        }

        private AppSettings MigrateSettings(AppSettings existingSettings)
        {
            var defaultSettings = new AppSettings();

            // Check if ShowInSystemTray needs migration (from old default false to new default true)
            // We'll assume any existing setting with false should be migrated to true for better UX
            if (!existingSettings.ShowInSystemTray && !HasExplicitlyDisabledTray(existingSettings))
            {
                existingSettings.ShowInSystemTray = defaultSettings.ShowInSystemTray; // true
                System.Diagnostics.Debug.WriteLine("Migrated ShowInSystemTray to new default (true)");
                
                // Save the migrated settings immediately
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
                    string json = JsonConvert.SerializeObject(existingSettings, Formatting.Indented);
                    File.WriteAllText(_settingsFilePath, json);
                    System.Diagnostics.Debug.WriteLine("Migration changes saved to settings file");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving migrated settings: {ex.Message}");
                }
            }

            return existingSettings;
        }

        private bool HasExplicitlyDisabledTray(AppSettings settings)
        {
            // If user has other UI customizations, they probably made conscious choices
            // In this case, respect their tray setting
            return settings.HideTitleBar || settings.AlwaysOnTop;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
                
                _currentSettings = settings.Clone();
                SettingsChanged?.Invoke(this, _currentSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public void UpdateSettings(Action<AppSettings> updateAction)
        {
            var settingsCopy = _currentSettings.Clone();
            updateAction(settingsCopy);
            SaveSettings(settingsCopy);
        }

        public void ResetToDefaults()
        {
            SaveSettings(new AppSettings());
        }
    }
} 