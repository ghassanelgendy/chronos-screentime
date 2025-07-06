using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace chronos_screentime.Windows
{
    public partial class CategorySelectionDialog : Window
    {
        public string? SelectedCategory { get; private set; }

        public CategorySelectionDialog(IEnumerable<string> categories, string title)
        {
            InitializeComponent();
            TitleText.Text = title;
            
            foreach (var category in categories)
            {
                CategoryListBox.Items.Add(category);
            }

            if (CategoryListBox.Items.Count > 0)
            {
                CategoryListBox.SelectedIndex = 0;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedCategory = CategoryListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(SelectedCategory))
            {
                MessageBox.Show("Please select a category.", "No Category Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
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