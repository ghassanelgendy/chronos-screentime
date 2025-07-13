using chronos_screentime.Services;
using System;
using System.Windows;
using System.Windows.Threading;

namespace chronos_screentime.Windows
{
    public partial class PowerActionConfirmationDialog : Window
    {
        private readonly PowerSchedulingService _powerSchedulingService;
        private readonly PowerSchedulingService.PowerAction _action;
        private readonly DispatcherTimer _countdownTimer;
        private int _countdownSeconds;
        private bool _isExecuting = false;

        public PowerActionConfirmationDialog(PowerSchedulingService powerSchedulingService, PowerSchedulingService.PowerAction action, int countdownSeconds = 30)
        {
            InitializeComponent();
            _powerSchedulingService = powerSchedulingService;
            _action = action;
            _countdownSeconds = countdownSeconds;

            // Set up countdown timer
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;

            // Initialize UI
            InitializeUI();

            // Start countdown
            _countdownTimer.Start();
        }

        private void InitializeUI()
        {
            var actionText = _action == PowerSchedulingService.PowerAction.Shutdown ? "power off" : "restart";
            
            TitleTextBlock.Text = $"Power Action Confirmation";
            SubtitleTextBlock.Text = $"Your computer will {actionText} in {_countdownSeconds} seconds";
            ActionDetailsTextBlock.Text = $"Your computer will be {actionText} automatically.";
            
            CountdownTextBlock.Text = _countdownSeconds.ToString();
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _countdownSeconds--;
            CountdownTextBlock.Text = _countdownSeconds.ToString();

            if (_countdownSeconds <= 0)
            {
                _countdownTimer.Stop();
                ExecuteAction();
            }
        }

        private async void ExecuteAction()
        {
            if (_isExecuting) return;
            _isExecuting = true;

            try
            {
                // Clear the current schedule to prevent double execution
                _powerSchedulingService.ClearSchedule();
                
                // Execute the power action
                await _powerSchedulingService.ExecutePowerAction(_action);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing power action: {ex.Message}");
                MessageBox.Show($"Error executing power action: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _countdownTimer.Stop();
                _powerSchedulingService.ClearSchedule();
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cancelling power action: {ex.Message}");
                MessageBox.Show($"Error cancelling power action: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _countdownTimer.Stop();
                ExecuteAction();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing power action now: {ex.Message}");
                MessageBox.Show($"Error executing power action: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _countdownTimer?.Stop();
            base.OnClosed(e);
        }
    }
} 