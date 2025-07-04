using System.Windows;
using chronos_screentime.Services;

namespace chronos_screentime.Windows
{
    public partial class UpdateProgressWindow : Window
    {
        private readonly UpdateInfo _updateInfo;

        public UpdateProgressWindow(UpdateInfo updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            
            VersionText.Text = $"Version: {updateInfo.Version}";
            StatusText.Text = "Starting download...";
        }

        public void UpdateProgress(double progress)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = progress * 100;
                ProgressText.Text = $"{progress * 100:F1}%";
                
                if (progress >= 1.0)
                {
                    StatusText.Text = "Download complete! Installing...";
                }
                else
                {
                    StatusText.Text = "Downloading...";
                }
            });
        }
    }
} 