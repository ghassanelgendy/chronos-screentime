using System.Windows;

namespace chronos_screentime.Windows
{
    public partial class TemplateViewerWindow : Window
    {
        public TemplateViewerWindow(string templateContent)
        {
            InitializeComponent();
            TemplateContentText.Text = templateContent;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
} 