using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using Wpf.Ui.Controls;
using System;
using System.Windows.Media;
using chronos_screentime;

namespace chronos_screentime.Services
{
    public interface IDialogService
    {
        Task<ContentDialogResult> ShowContentDialogAsync(string title, string content, string primaryButtonText = "OK", string? secondaryButtonText = null, string? closeButtonText = null);
        Task<bool> ShowConfirmationDialogAsync(string title, string message);
        Task ShowInfoDialogAsync(string title, string message);
        Task ShowErrorDialogAsync(string title, string message);
    }

    public class DialogService : IDialogService
    {
        public async Task<ContentDialogResult> ShowContentDialogAsync(
            string title,
            string content,
            string primaryButtonText = "OK",
            string? secondaryButtonText = null,
            string? closeButtonText = null)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                throw new InvalidOperationException("Main window not found");
            }

            // Create a popup layer Grid that overlays the entire window
            var popupLayerGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
            };

            // Create content presenter for the dialog and add it to the popup layer
            var contentPresenter = new System.Windows.Controls.ContentPresenter();
            popupLayerGrid.Children.Add(contentPresenter);

            // Create a popup window that covers the main window
            var popup = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = mainWindow,
                Content = popupLayerGrid,
                Width = mainWindow.ActualWidth,
                Height = mainWindow.ActualHeight,
                Left = mainWindow.Left,
                Top = mainWindow.Top,
                WindowState = mainWindow.WindowState,
                Topmost = true
            };

            var dialog = new Wpf.Ui.Controls.ContentDialog
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = primaryButtonText,
                DialogHost = contentPresenter
            };

            if (secondaryButtonText != null)
            {
                dialog.SecondaryButtonText = secondaryButtonText;
            }

            if (closeButtonText != null)
            {
                dialog.CloseButtonText = closeButtonText;
            }

            // Handle window state changes
            mainWindow.LocationChanged += (s, e) =>
            {
                popup.Left = mainWindow.Left;
                popup.Top = mainWindow.Top;
            };

            mainWindow.SizeChanged += (s, e) =>
            {
                popup.Width = mainWindow.ActualWidth;
                popup.Height = mainWindow.ActualHeight;
                popup.WindowState = mainWindow.WindowState;
            };

            // Show the popup
            popup.Show();

            try
            {
                return await dialog.ShowAsync();
            }
            finally
            {
                // Clean up
                mainWindow.LocationChanged -= (s, e) =>
                {
                    popup.Left = mainWindow.Left;
                    popup.Top = mainWindow.Top;
                };

                mainWindow.SizeChanged -= (s, e) =>
                {
                    popup.Width = mainWindow.ActualWidth;
                    popup.Height = mainWindow.ActualHeight;
                    popup.WindowState = mainWindow.WindowState;
                };

                popup.Close();
            }
        }

        public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            var result = await ShowContentDialogAsync(title, message, "Yes", "No");
            return result == ContentDialogResult.Primary;
        }

        public async Task ShowInfoDialogAsync(string title, string message)
        {
            await ShowContentDialogAsync(title, message, "OK");
        }

        public async Task ShowErrorDialogAsync(string title, string message)
        {
            await ShowContentDialogAsync(title, message, "OK");
        }
    }
}