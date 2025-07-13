using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace chronos_screentime.Services
{
    public class PowerSchedulingService
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool InitiateSystemShutdownEx(
            string lpMachineName,
            string lpMessage,
            uint dwTimeout,
            bool bForceAppsClosed,
            bool bRebootAfterShutdown);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AbortSystemShutdown(string lpMachineName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetSystemPowerState(bool fSuspend, bool fForce);

        private CancellationTokenSource? _scheduledActionCancellation;
        private DateTime? _scheduledTime;
        private DateTime? _originalScheduledTime;
        private PowerAction _scheduledAction;
        private bool _isScheduled = false;
        private bool _isPaused = false;
        private TimeSpan _pausedTimeRemaining;

        public enum PowerAction
        {
            Shutdown,
            Restart
        }

        public class PowerScheduleInfo
        {
            public bool IsScheduled { get; set; }
            public DateTime? ScheduledTime { get; set; }
            public PowerAction Action { get; set; }
            public TimeSpan TimeRemaining { get; set; }
            public bool IsPaused { get; set; }
            public TimeSpan PausedTimeRemaining { get; set; }
        }

        public event EventHandler<PowerScheduleInfo>? ScheduleChanged;

        public PowerScheduleInfo GetCurrentSchedule()
        {
            if (!_isScheduled || !_scheduledTime.HasValue)
            {
                return new PowerScheduleInfo
                {
                    IsScheduled = false,
                    ScheduledTime = null,
                    Action = PowerAction.Shutdown,
                    TimeRemaining = TimeSpan.Zero,
                    IsPaused = false,
                    PausedTimeRemaining = TimeSpan.Zero
                };
            }

            if (_isPaused)
            {
                return new PowerScheduleInfo
                {
                    IsScheduled = true,
                    ScheduledTime = _scheduledTime.Value,
                    Action = _scheduledAction,
                    TimeRemaining = _pausedTimeRemaining,
                    IsPaused = true,
                    PausedTimeRemaining = _pausedTimeRemaining
                };
            }

            var timeRemaining = _scheduledTime.Value - DateTime.Now;
            if (timeRemaining <= TimeSpan.Zero)
            {
                // Schedule has expired, clear it
                ClearSchedule();
                return new PowerScheduleInfo
                {
                    IsScheduled = false,
                    ScheduledTime = null,
                    Action = PowerAction.Shutdown,
                    TimeRemaining = TimeSpan.Zero,
                    IsPaused = false,
                    PausedTimeRemaining = TimeSpan.Zero
                };
            }

            return new PowerScheduleInfo
            {
                IsScheduled = true,
                ScheduledTime = _scheduledTime.Value,
                Action = _scheduledAction,
                TimeRemaining = timeRemaining,
                IsPaused = false,
                PausedTimeRemaining = TimeSpan.Zero
            };
        }

        public async Task<bool> SchedulePowerAction(PowerAction action, TimeSpan delay)
        {
            try
            {
                // Cancel any existing schedule
                ClearSchedule();

                if (delay <= TimeSpan.Zero)
                {
                    // Execute immediately
                    return await ExecutePowerAction(action);
                }

                _scheduledTime = DateTime.Now.Add(delay);
                _originalScheduledTime = _scheduledTime;
                _scheduledAction = action;
                _isScheduled = true;
                _isPaused = false;
                _scheduledActionCancellation = new CancellationTokenSource();

                // Calculate when to show confirmation dialog (30 seconds before execution)
                var confirmationDelay = delay - TimeSpan.FromSeconds(30);
                var finalDelay = TimeSpan.FromSeconds(30);
                
                // If delay is less than 30 seconds, show confirmation immediately
                if (confirmationDelay <= TimeSpan.Zero)
                {
                    confirmationDelay = TimeSpan.Zero;
                    finalDelay = delay;
                }
                
                // Start the countdown timer
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Wait until 30 seconds before execution (or immediately if delay < 30s)
                        if (confirmationDelay > TimeSpan.Zero)
                        {
                            await Task.Delay(confirmationDelay, _scheduledActionCancellation.Token);
                        }
                        
                        // Show confirmation dialog on UI thread
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            if (_isScheduled && !_isPaused)
                            {
                                try
                                {
                                    var confirmationDialog = new Windows.PowerActionConfirmationDialog(this, action, (int)finalDelay.TotalSeconds);
                                    confirmationDialog.ShowDialog();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error showing confirmation dialog: {ex.Message}");
                                    // If dialog fails, continue with execution
                                    await ExecutePowerAction(_scheduledAction);
                                    ClearSchedule();
                                }
                            }
                        });
                        
                        // Wait for the remaining time or until cancelled
                        await Task.Delay(finalDelay, _scheduledActionCancellation.Token);
                        
                        // If we reach here, the delay has completed and user didn't cancel
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            if (_isScheduled && !_isPaused)
                            {
                                await ExecutePowerAction(_scheduledAction);
                                ClearSchedule();
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        // Schedule was cancelled
                    }
                });

                // Notify subscribers
                ScheduleChanged?.Invoke(this, GetCurrentSchedule());

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scheduling power action: {ex.Message}");
                return false;
            }
        }

        public void ClearSchedule()
        {
            if (_scheduledActionCancellation != null)
            {
                _scheduledActionCancellation.Cancel();
                _scheduledActionCancellation.Dispose();
                _scheduledActionCancellation = null;
            }

            _isScheduled = false;
            _isPaused = false;
            _scheduledTime = null;
            _originalScheduledTime = null;
            _pausedTimeRemaining = TimeSpan.Zero;

            // Notify subscribers
            ScheduleChanged?.Invoke(this, GetCurrentSchedule());
        }

        public void PauseTimer()
        {
            if (!_isScheduled || _isPaused || !_scheduledTime.HasValue)
                return;

            _pausedTimeRemaining = _scheduledTime.Value - DateTime.Now;
            if (_pausedTimeRemaining <= TimeSpan.Zero)
            {
                ClearSchedule();
                return;
            }

            _isPaused = true;

            // Cancel the current timer
            if (_scheduledActionCancellation != null)
            {
                _scheduledActionCancellation.Cancel();
                _scheduledActionCancellation.Dispose();
                _scheduledActionCancellation = null;
            }

            // Notify subscribers
            ScheduleChanged?.Invoke(this, GetCurrentSchedule());
        }

        public void ResumeTimer()
        {
            if (!_isScheduled || !_isPaused)
                return;

            _isPaused = false;
            _scheduledTime = DateTime.Now.Add(_pausedTimeRemaining);
            _scheduledActionCancellation = new CancellationTokenSource();

            // Calculate when to show confirmation dialog (30 seconds before execution)
            var confirmationDelay = _pausedTimeRemaining - TimeSpan.FromSeconds(30);
            var finalDelay = TimeSpan.FromSeconds(30);
            
            // If delay is less than 30 seconds, show confirmation immediately
            if (confirmationDelay <= TimeSpan.Zero)
            {
                confirmationDelay = TimeSpan.Zero;
                finalDelay = _pausedTimeRemaining;
            }

            // Restart the countdown timer
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait until 30 seconds before execution (or immediately if delay < 30s)
                    if (confirmationDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(confirmationDelay, _scheduledActionCancellation.Token);
                    }
                    
                    // Show confirmation dialog on UI thread
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (_isScheduled && !_isPaused)
                        {
                            try
                            {
                                var confirmationDialog = new Windows.PowerActionConfirmationDialog(this, _scheduledAction, (int)finalDelay.TotalSeconds);
                                confirmationDialog.ShowDialog();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error showing confirmation dialog: {ex.Message}");
                                // If dialog fails, continue with execution
                                await ExecutePowerAction(_scheduledAction);
                                ClearSchedule();
                            }
                        }
                    });
                    
                    // Wait for the remaining time or until cancelled
                    await Task.Delay(finalDelay, _scheduledActionCancellation.Token);
                    
                    // If we reach here, the delay has completed and user didn't cancel
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (_isScheduled && !_isPaused)
                        {
                            await ExecutePowerAction(_scheduledAction);
                            ClearSchedule();
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // Schedule was cancelled
                }
            });

            // Notify subscribers
            ScheduleChanged?.Invoke(this, GetCurrentSchedule());
        }

        public async Task<bool> ExecutePowerAction(PowerAction action)
        {
            try
            {
                switch (action)
                {
                    case PowerAction.Shutdown:
                        return await ShutdownSystem();
                    case PowerAction.Restart:
                        return await RestartSystem();
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing power action: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ShutdownSystem()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use shutdown command for more control
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "shutdown";
                    process.StartInfo.Arguments = "/s /t 0";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    
                    return process.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error shutting down system: {ex.Message}");
                    return false;
                }
            });
        }

        private async Task<bool> RestartSystem()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use shutdown command for restart
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "shutdown";
                    process.StartInfo.Arguments = "/r /t 0";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    
                    return process.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error restarting system: {ex.Message}");
                    return false;
                }
            });
        }

        public bool AbortScheduledShutdown()
        {
            try
            {
                return AbortSystemShutdown(string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aborting shutdown: {ex.Message}");
                return false;
            }
        }
    }
} 