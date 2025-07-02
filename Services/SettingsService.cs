using chronos_screentime.Models;
using Newtonsoft.Json;
using System.IO;

namespace chronos_screentime.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private AppSettings _currentSettings;

        public event EventHandler<AppSettings>? SettingsChanged;

        public AppSettings CurrentSettings
        {
            get => _currentSettings;
            set => _currentSettings = value;
        }

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
                        System.Diagnostics.Debug.WriteLine($"Settings loaded from {_settingsFilePath}");
                        System.Diagnostics.Debug.WriteLine($"Loaded settings - ShowInTray: {settings.ShowInSystemTray}, AlwaysOnTop: {settings.AlwaysOnTop}, Theme: {settings.Theme}");

                        // Migrate existing settings to ensure new defaults are applied
                        var migratedSettings = MigrateSettings(settings);
                        return migratedSettings;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Settings file not found at {_settingsFilePath}, using defaults");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            // Return default settings if loading fails
            var defaultSettings = new AppSettings();
            System.Diagnostics.Debug.WriteLine($"Using default settings - ShowInTray: {defaultSettings.ShowInSystemTray}, AlwaysOnTop: {defaultSettings.AlwaysOnTop}, Theme: {defaultSettings.Theme}");
            return defaultSettings;
        }

        private AppSettings MigrateSettings(AppSettings existingSettings)
        {
            // Simply return existing settings without any automatic migrations
            // User preferences should be respected and not overridden
            System.Diagnostics.Debug.WriteLine("Settings loaded without migration - preserving user preferences");
            return existingSettings;
        }

        public void SaveSettings(AppSettings settings = null)
        {
            if (settings != null)
            {
                _currentSettings = settings;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
                string json = JsonConvert.SerializeObject(CurrentSettings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Settings saved to {_settingsFilePath}");
                System.Diagnostics.Debug.WriteLine($"Saved settings - ShowInTray: {CurrentSettings.ShowInSystemTray}, AlwaysOnTop: {CurrentSettings.AlwaysOnTop}, Theme: {CurrentSettings.Theme}");

                SettingsChanged?.Invoke(this, CurrentSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public void UpdateSettings(Action<AppSettings> updateAction)
        {
            var settingsCopy = CurrentSettings.Clone();
            updateAction(settingsCopy);
            SaveSettings(settingsCopy);
        }

        public void ResetToDefaults()
        {
            SaveSettings(new AppSettings());
        }
    }
}