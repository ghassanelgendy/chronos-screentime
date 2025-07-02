using chronos_screentime.Models;
using Newtonsoft.Json;
using System.IO;

namespace chronos_screentime.Services
{
    public class SettingsService
    {
        private const string SettingsFileName = "settings.json";
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
                "Chronos",
                SettingsFileName);

            _currentSettings = LoadSettings();
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                return new AppSettings();
            }
        }

        private AppSettings MigrateSettings(AppSettings existingSettings)
        {
            // Simply return existing settings without any automatic migrations
            // User preferences should be respected and not overridden
            System.Diagnostics.Debug.WriteLine("Settings loaded without migration - preserving user preferences");
            return existingSettings;
        }

        public void SaveSettings(AppSettings? settings = null)
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