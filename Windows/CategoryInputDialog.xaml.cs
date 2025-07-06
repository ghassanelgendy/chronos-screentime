using System.Windows;

namespace chronos_screentime.Windows
{
    public partial class CategoryInputDialog : Window
    {
        public string? CategoryName { get; private set; }

        public CategoryInputDialog(string title, string initialValue)
        {
            InitializeComponent();
            TitleText.Text = title;
            CategoryNameTextBox.Text = initialValue;
            CategoryNameTextBox.Focus();
            CategoryNameTextBox.SelectAll();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            CategoryName = CategoryNameTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(CategoryName))
            {
                MessageBox.Show("Please enter a category name.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 