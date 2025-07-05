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
                "ChronosScreenTime",
                SettingsFileName);

            _currentSettings = LoadSettings();
        }

        public AppSettings LoadSettings()
        {
            try
            {
                AppSettings settings;
                
                if (!File.Exists(_settingsFilePath))
                {
                    settings = new AppSettings();
                }
                else
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }

                // Sync startup setting with registry on load
                SyncStartupSettingOnLoad(settings);
                
                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// Syncs the startup setting from registry when loading settings
        /// </summary>
        private void SyncStartupSettingOnLoad(AppSettings settings)
        {
            try
            {
                var registryStartupSetting = StartupService.GetStartupSetting();
                
                // Update the setting to match the registry if they're different
                if (settings.StartWithWindows != registryStartupSetting)
                {
                    settings.StartWithWindows = registryStartupSetting;
                    System.Diagnostics.Debug.WriteLine($"Startup setting synced from registry: {registryStartupSetting}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing startup setting on load: {ex.Message}");
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

                // Sync startup setting with Windows registry
                SyncStartupSetting();

                SettingsChanged?.Invoke(this, CurrentSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Syncs the StartWithWindows setting with the Windows registry
        /// </summary>
        private void SyncStartupSetting()
        {
            try
            {
                var registryStartupSetting = StartupService.GetStartupSetting();
                
                if (CurrentSettings.StartWithWindows != registryStartupSetting)
                {
                    // Setting differs from registry - update registry
                    StartupService.SetStartupOption(CurrentSettings.StartWithWindows);
                    System.Diagnostics.Debug.WriteLine($"Startup setting synced: {CurrentSettings.StartWithWindows} in registry");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing startup setting: {ex.Message}");
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