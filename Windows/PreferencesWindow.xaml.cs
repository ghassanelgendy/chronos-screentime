using System;
using System.Windows;

namespace chronos_screentime.Windows
{
    public partial class PreferencesWindow : Window
    {
        public PreferencesWindow()
        {
            InitializeComponent();
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = ThemedMessageBox.Show(
                this,
                "Are you sure you want to reset all preferences to default values?",
                "Reset to Defaults",
                ThemedMessageBox.MessageButtons.YesNo,
                ThemedMessageBox.MessageType.Question);

            if (result == MessageBoxResult.Yes)
            {
                // TODO: Reset all preferences to default values
                ThemedMessageBox.Show(this, "Preferences have been reset to defaults.", "Reset Complete", 
                              ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Apply all preference changes without closing window
            ThemedMessageBox.Show(this, "Preferences applied successfully.", "Apply", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Apply all preference changes and close window
            ThemedMessageBox.Show(this, "Preferences saved successfully.", "Preferences Saved", 
                          ThemedMessageBox.MessageButtons.OK, ThemedMessageBox.MessageType.Information);
            this.Close();
        }
    }
} 