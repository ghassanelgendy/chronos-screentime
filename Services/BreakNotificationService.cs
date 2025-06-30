using System;
using System.Timers;
using chronos_screentime.Models;

namespace chronos_screentime.Services
{
    public class BreakNotificationService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly Action<string, string>? _showNotificationCallback;
        private readonly Func<bool>? _isWindowMinimizedCallback;
        private System.Timers.Timer? _breakReminderTimer;
        private System.Timers.Timer? _screenBreakTimer;
        private DateTime _lastBreakNotification;
        private DateTime _lastScreenBreakNotification;

        public BreakNotificationService(SettingsService settingsService, Action<string, string>? showNotificationCallback = null, Func<bool>? isWindowMinimizedCallback = null)
        {
            _settingsService = settingsService;
            _showNotificationCallback = showNotificationCallback;
            _isWindowMinimizedCallback = isWindowMinimizedCallback;
            _lastBreakNotification = DateTime.Now;
            _lastScreenBreakNotification = DateTime.Now;

            // Subscribe to settings changes
            _settingsService.SettingsChanged += OnSettingsChanged;
            
            // Initialize timers based on current settings
            UpdateTimers();
        }

        private void OnSettingsChanged(object? sender, AppSettings settings)
        {
            UpdateTimers();
        }

        private void UpdateTimers()
        {
            var settings = _settingsService.CurrentSettings;
            
            // Update break reminder timer
            UpdateBreakReminderTimer(settings);
            
            // Update screen break timer
            UpdateScreenBreakTimer(settings);
        }

        private void UpdateBreakReminderTimer(AppSettings settings)
        {
            _breakReminderTimer?.Stop();
            _breakReminderTimer?.Dispose();
            _breakReminderTimer = null;

            if (settings.EnableBreakNotifications && settings.BreakReminderMinutes > 0)
            {
                var intervalMs = settings.BreakReminderMinutes * 60 * 1000; // Convert to milliseconds
                _breakReminderTimer = new System.Timers.Timer(intervalMs);
                _breakReminderTimer.Elapsed += OnBreakReminderElapsed;
                _breakReminderTimer.AutoReset = true;
                _breakReminderTimer.Start();
                
                System.Diagnostics.Debug.WriteLine($"Break reminder timer started - every {settings.BreakReminderMinutes} minutes");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Break reminder timer disabled");
            }
        }

        private void UpdateScreenBreakTimer(AppSettings settings)
        {
            _screenBreakTimer?.Stop();
            _screenBreakTimer?.Dispose();
            _screenBreakTimer = null;

            if (settings.EnableScreenBreakNotifications && settings.ScreenBreakReminderMinutes > 0)
            {
                _screenBreakTimer = new System.Timers.Timer(settings.ScreenBreakReminderMinutes * 60 * 1000); // Convert to milliseconds
                _screenBreakTimer.Elapsed += OnScreenBreakElapsed;
                _screenBreakTimer.AutoReset = true;
                _screenBreakTimer.Start();
                
                System.Diagnostics.Debug.WriteLine($"Screen break timer started - every {settings.ScreenBreakReminderMinutes} minutes");
            }
        }

        private void OnBreakReminderElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // Timer already handles timing, just show notification
                ShowBreakReminder();
                _lastBreakNotification = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"Break reminder timer fired at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in break reminder: {ex.Message}");
            }
        }

        private void OnScreenBreakElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // Timer already handles timing, just show notification
                ShowScreenBreakReminder();
                _lastScreenBreakNotification = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"Screen break timer fired at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in screen break reminder: {ex.Message}");
            }
        }

        private void ShowBreakReminder()
        {
            var settings = _settingsService.CurrentSettings;
            
            try
            {
                _showNotificationCallback?.Invoke(
                    "Break Time!",
                    $"You've been working for {settings.BreakReminderMinutes} minutes. Time to take a break!"
                );

                // Play sound if enabled AND window is not minimized
                if (settings.PlaySoundForNotifications && (_isWindowMinimizedCallback?.Invoke() != true))
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }

                System.Diagnostics.Debug.WriteLine("Break reminder notification sent");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing break reminder: {ex.Message}");
            }
        }

        private void ShowScreenBreakReminder()
        {
            var settings = _settingsService.CurrentSettings;
            
            try
            {
                _showNotificationCallback?.Invoke(
                    "Screen Break - 20-20-20 Rule!",
                    $"Look at something 20 feet away for {settings.ScreenBreakDurationSeconds} seconds to rest your eyes."
                );

                // Play sound if enabled AND window is not minimized
                if (settings.PlaySoundWithBreakReminder && (_isWindowMinimizedCallback?.Invoke() != true))
                {
                    System.Media.SystemSounds.Exclamation.Play();
                }

                System.Diagnostics.Debug.WriteLine("Screen break reminder notification sent");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing screen break reminder: {ex.Message}");
            }
        }

        public void ResetBreakTimer()
        {
            _lastBreakNotification = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("Break timer reset");
        }

        public void ResetScreenBreakTimer()
        {
            _lastScreenBreakNotification = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("Screen break timer reset");
        }

        public void ResetAllTimers()
        {
            ResetBreakTimer();
            ResetScreenBreakTimer();
        }

        public void Dispose()
        {
            _breakReminderTimer?.Stop();
            _breakReminderTimer?.Dispose();
            _screenBreakTimer?.Stop();
            _screenBreakTimer?.Dispose();
            
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }
        }
    }
} 