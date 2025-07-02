using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using Wpf.Ui.Controls;

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
            var dialog = new Wpf.Ui.Controls.ContentDialog
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = primaryButtonText
            };

            if (secondaryButtonText != null)
            {
                dialog.SecondaryButtonText = secondaryButtonText;
            }

            if (closeButtonText != null)
            {
                dialog.CloseButtonText = closeButtonText;
            }

            return await dialog.ShowAsync();
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