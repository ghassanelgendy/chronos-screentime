using chronos_screentime.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace chronos_screentime.Windows
{
    public partial class PowerSchedulingDialog : Wpf.Ui.Controls.FluentWindow
    {
        private readonly PowerSchedulingService _powerSchedulingService;
        private readonly System.Windows.Threading.DispatcherTimer _updateTimer;

        public PowerSchedulingDialog(PowerSchedulingService powerSchedulingService)
        {
            InitializeComponent();
            _powerSchedulingService = powerSchedulingService;

            // Subscribe to schedule changes
            _powerSchedulingService.ScheduleChanged += OnScheduleChanged;

            // Set up timer to update status
            _updateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateStatus;
            _updateTimer.Start();

            // Load current schedule status
            UpdateStatusDisplay();

            // Set focus to hours input
            HoursNumberBox.Focus();
        }

        private void OnScheduleChanged(object? sender, PowerSchedulingService.PowerScheduleInfo e)
        {
            // Update UI on the UI thread
            Dispatcher.Invoke(() => UpdateStatusDisplay());
        }

        private void UpdateStatusDisplay()
        {
            var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
            
            if (scheduleInfo.IsScheduled)
            {
                StatusCard.Visibility = Visibility.Visible;
                CancelCurrentScheduleButton.Visibility = Visibility.Visible;
                var actionText = scheduleInfo.Action == PowerSchedulingService.PowerAction.Shutdown ? "Power Off" : "Restart";
                var timeText = FormatTimeSpan(scheduleInfo.TimeRemaining);
                
                if (scheduleInfo.IsPaused)
                {
                    StatusTextBlock.Text = $"Scheduled to {actionText} (PAUSED)";
                    CountdownTextBlock.Text = $"⏸️ Paused: {timeText} remaining";
                    StopTimerButton.Visibility = Visibility.Collapsed;
                    ResumeTimerButton.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusTextBlock.Text = $"Scheduled to {actionText}";
                    CountdownTextBlock.Text = $"⏰ Time remaining: {timeText}";
                    StopTimerButton.Visibility = Visibility.Visible;
                    ResumeTimerButton.Visibility = Visibility.Collapsed;
                }
                
                // Update button text
                ScheduleButton.Content = "Reschedule";
            }
            else
            {
                StatusCard.Visibility = Visibility.Collapsed;
                CancelCurrentScheduleButton.Visibility = Visibility.Collapsed;
                ScheduleButton.Content = "Schedule Now";
            }
        }

        private void UpdateStatus(object? sender, EventArgs e)
        {
            var scheduleInfo = _powerSchedulingService.GetCurrentSchedule();
            if (scheduleInfo.IsScheduled)
            {
                var actionText = scheduleInfo.Action == PowerSchedulingService.PowerAction.Shutdown ? "Power Off" : "Restart";
                var timeText = FormatTimeSpan(scheduleInfo.TimeRemaining);
                
                if (scheduleInfo.IsPaused)
                {
                    StatusTextBlock.Text = $"Scheduled to {actionText} (PAUSED)";
                    CountdownTextBlock.Text = $"⏸️ Paused: {timeText} remaining";
                }
                else
                {
                    StatusTextBlock.Text = $"Scheduled to {actionText}";
                    CountdownTextBlock.Text = $"⏰ Time remaining: {timeText}";
                }
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

        private async void Schedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the selected action
                var action = ShutdownRadioButton.IsChecked == true 
                    ? PowerSchedulingService.PowerAction.Shutdown 
                    : PowerSchedulingService.PowerAction.Restart;

                // Calculate the delay
                var hours = (int)(HoursNumberBox.Value ?? 0);
                var minutes = (int)(MinutesNumberBox.Value ?? 0);
                var delay = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);

                if (delay <= TimeSpan.Zero)
                {
                    MessageBox.Show("Please enter a valid time greater than 0.", "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show confirmation dialog
                var actionText = action == PowerSchedulingService.PowerAction.Shutdown ? "power off" : "restart";
                var timeText = FormatTimeSpan(delay);
                var result = MessageBox.Show(
                    $"Are you sure you want to schedule your computer to {actionText} in {timeText}?\n\nYou can cancel this schedule at any time.",
                    "Confirm Schedule",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Schedule the action
                    var success = await _powerSchedulingService.SchedulePowerAction(action, delay);
                    
                    if (success)
                    {
                        var actionDisplayText = action == PowerSchedulingService.PowerAction.Shutdown ? "Power Off" : "Restart";
                        MessageBox.Show(
                            $"Computer scheduled to {actionDisplayText} in {timeText}.\n\nYou can cancel this schedule using the 'Cancel Schedule' button.",
                            "Schedule Confirmed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                            
                        // Close the dialog after successful scheduling
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to schedule the power action. Please try again.",
                            "Schedule Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error scheduling power action: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to cancel the scheduled power action?",
                    "Cancel Schedule",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _powerSchedulingService.ClearSchedule();
                    MessageBox.Show("Scheduled power action has been cancelled.", "Schedule Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Close the dialog after successfully cancelling the schedule
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cancelling schedule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QuickPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                if (int.TryParse(tag, out int minutes))
                {
                    var hours = minutes / 60;
                    var remainingMinutes = minutes % 60;
                    
                    HoursNumberBox.Value = hours;
                    MinutesNumberBox.Value = remainingMinutes;
                }
            }
        }

        private void StopTimer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _powerSchedulingService.PauseTimer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pausing timer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResumeTimer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _powerSchedulingService.ResumeTimer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resuming timer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Reschedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear current schedule first
                _powerSchedulingService.ClearSchedule();
                
                // Focus on the time inputs for rescheduling
                HoursNumberBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error preparing for reschedule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up
            _powerSchedulingService.ScheduleChanged -= OnScheduleChanged;
            _updateTimer?.Stop();
            base.OnClosed(e);
        }
    }
} 