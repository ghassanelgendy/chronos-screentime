using chronos_screentime.Models;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;

namespace chronos_screentime.Services
{
    public class BreakNotificationService : IDisposable
    {
        // Windows API for volume control
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutGetVolume(IntPtr hwo, out uint dwVolume);
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
                var breakQuotes = new[]
                {
                    $"You've been working for {settings.BreakReminderMinutes} minutes. Time to take a break!",
                    "A short break can boost your productivity. Take a moment to recharge!",
                    "Your mind needs rest to stay sharp. How about a quick break?",
                    "Regular breaks help maintain focus. Time to step away for a moment!",
                    "Take care of yourself - it's break time!",
                    "A refreshed mind is a productive mind. Time for a quick break!",
                    "You've earned it! Step away and let your brain breathe for a bit.",
                    "Break time! Stretch, sip, and smile – you've got this.",
                    "Even superheroes need a pause. Take yours now!",
                    "Rest isn't lazy – it's strategic. Time to recharge.",
                    "Step back for a second – your best ideas are waiting on the other side of this break.",
                    "Refocus, refresh, return stronger. Break time!",
                    "Let your eyes wander, your thoughts drift. A quick pause can spark brilliance.",
                    "The best work comes from a rested mind. Time to hit pause!",
                    "Your focus will thank you later. Take five!",
                    "Small breaks = big wins. Time to refuel your energy."
                };

                var random = new Random();
                var selectedQuote = breakQuotes[random.Next(breakQuotes.Length)];

                // Marshal to UI thread for all UI operations
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Break reminder: PlaySoundForNotifications={settings.PlaySoundForNotifications}, NotificationSoundFile={settings.NotificationSoundFile}");

                        // Show visual notification (handled by callback)
                        _showNotificationCallback?.Invoke("Break Reminder", selectedQuote);

                        // Play custom sound if enabled (with 3-second delay)
                        if (settings.PlaySoundForNotifications)
                        {
                            string wavDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "wav");
                            string soundFile = settings.NotificationSoundFile ?? "sneeze.wav";
                            string soundPath = Path.Combine(wavDir, soundFile);

                            System.Diagnostics.Debug.WriteLine($"Notification shown, will play sound in 3 seconds: {soundPath} at {settings.NotificationVolume}% volume");
                            System.Diagnostics.Debug.WriteLine($"Sound file exists: {File.Exists(soundPath)}");

                            // Create a timer to delay sound playback by 3 seconds
                            var soundTimer = new System.Threading.Timer(state =>
                            {
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        if (File.Exists(soundPath))
                                        {
                                            try
                                            {
                                                PlaySoundWithVolume(soundPath, settings.NotificationVolume);
                                                System.Diagnostics.Debug.WriteLine($"Successfully played sound after 3s delay: {soundFile} at {settings.NotificationVolume}% volume");
                                            }
                                            catch (Exception soundEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Error playing sound: {soundEx.Message}");
                                                System.Media.SystemSounds.Asterisk.Play(); // fallback
                                            }
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Sound file not found, playing fallback");
                                            System.Media.SystemSounds.Asterisk.Play(); // fallback
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error in delayed sound playback: {ex.Message}");
                                    }
                                });
                            }, null, 3000, Timeout.Infinite); // 3 seconds delay, single execution

                            // Clean up timer after a short delay
                            var cleanupTimer = new System.Threading.Timer(state =>
                            {
                                soundTimer?.Dispose();
                            }, null, 5000, Timeout.Infinite); // Cleanup after 5 seconds
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Sound disabled in settings");
                        }

                        System.Diagnostics.Debug.WriteLine("Break reminder notification sent");
                    }
                    catch (Exception uiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in UI thread for break reminder: {uiEx.Message}");
                    }
                });
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
                var screenBreakQuotes = new[]
                {
                    "Time for a screen break! Your eyes will thank you.",
                    "Look away from the screen. Give your eyes a rest.",
                    "Screen break time! Look at something in the distance.",
                    "Take a moment to rest your eyes. Look away from the screen.",
                    "Your eyes need a break from the screen. Look around!",
                    "Time to give your eyes a vacation from pixels!",
                    "Screen break! Blink, look away, breathe easy.",
                    "Eyes getting tired? Perfect timing for a screen break!",
                    "Digital detox moment – look up, look around, look out!",
                    "Rest those hardworking eyes. Screen break time!"
                };

                var random = new Random();
                var selectedQuote = screenBreakQuotes[random.Next(screenBreakQuotes.Length)];

                // Marshal to UI thread for all UI operations
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Screen break reminder: PlaySoundWithBreakReminder={settings.PlaySoundWithBreakReminder}, NotificationSoundFile={settings.NotificationSoundFile}");

                        // Show visual notification (handled by callback)
                        _showNotificationCallback?.Invoke("Screen Break Reminder", selectedQuote);

                        // Play custom sound if enabled (with 3-second delay)
                        if (settings.PlaySoundWithBreakReminder)
                        {
                            string wavDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "wav");
                            string soundFile = settings.NotificationSoundFile ?? "sneeze.wav";
                            string soundPath = Path.Combine(wavDir, soundFile);

                            System.Diagnostics.Debug.WriteLine($"Screen break notification shown, will play sound in 3 seconds: {soundPath} at {settings.NotificationVolume}% volume");
                            System.Diagnostics.Debug.WriteLine($"Screen break sound file exists: {File.Exists(soundPath)}");

                            // Create a timer to delay sound playback by 3 seconds
                            var soundTimer = new System.Threading.Timer(state =>
                            {
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        if (File.Exists(soundPath))
                                        {
                                            try
                                            {
                                                PlaySoundWithVolume(soundPath, settings.NotificationVolume);
                                                System.Diagnostics.Debug.WriteLine($"Successfully played screen break sound after 3s delay: {soundFile} at {settings.NotificationVolume}% volume");
                                            }
                                            catch (Exception soundEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Error playing screen break sound: {soundEx.Message}");
                                                System.Media.SystemSounds.Exclamation.Play(); // fallback
                                            }
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Screen break sound file not found, playing fallback");
                                            System.Media.SystemSounds.Exclamation.Play(); // fallback
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error in delayed screen break sound playback: {ex.Message}");
                                    }
                                });
                            }, null, 2000, Timeout.Infinite); // 3 seconds delay, single execution

                            // Clean up timer after a short delay
                            var cleanupTimer = new System.Threading.Timer(state =>
                            {
                                soundTimer?.Dispose();
                            }, null, 5000, Timeout.Infinite); // Cleanup after 5 seconds
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Screen break sound disabled in settings");
                        }

                        System.Diagnostics.Debug.WriteLine("Screen break reminder notification sent");
                    }
                    catch (Exception uiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in UI thread for screen break reminder: {uiEx.Message}");
                    }
                });
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

        private void PlaySoundWithVolume(string soundPath, int volumePercent)
        {
            try
            {
                // Convert percentage (0-100) to Windows volume format (0x0000 to 0xFFFF for each channel)
                uint volume = (uint)((volumePercent / 100.0) * 0xFFFF);
                uint stereoVolume = (volume << 16) | volume; // Set both left and right channels

                // Get current system volume
                waveOutGetVolume(IntPtr.Zero, out uint originalVolume);

                try
                {
                    // Set temporary volume
                    waveOutSetVolume(IntPtr.Zero, stereoVolume);

                    // Play the sound
                    var soundPlayer = new SoundPlayer(soundPath);
                    soundPlayer.PlaySync(); // Use sync to ensure volume is restored after playback
                }
                finally
                {
                    // Restore original volume
                    waveOutSetVolume(IntPtr.Zero, originalVolume);
                }

                System.Diagnostics.Debug.WriteLine($"Played sound with {volumePercent}% volume");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound with volume: {ex.Message}");
                // Fallback to regular sound player
                try
                {
                    var soundPlayer = new SoundPlayer(soundPath);
                    soundPlayer.Play();
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback sound playback also failed: {fallbackEx.Message}");
                }
            }
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