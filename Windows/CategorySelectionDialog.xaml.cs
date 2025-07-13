using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace chronos_screentime.Windows
{
    public partial class CategorySelectionDialog : Window
    {
        public string? SelectedCategory { get; private set; }

        public CategorySelectionDialog(IEnumerable<string> categories, string title)
        {
            InitializeComponent();
            TitleText.Text = title;
            
            // Sort categories alphabetically for better UX
            var sortedCategories = categories.OrderBy(c => c).ToList();
            
            foreach (var category in sortedCategories)
            {
                CategoryListBox.Items.Add(category);
            }

            if (CategoryListBox.Items.Count > 0)
            {
                CategoryListBox.SelectedIndex = 0;
            }

            // Enable double-click to select
            CategoryListBox.MouseDoubleClick += CategoryListBox_MouseDoubleClick;
            
            // Set focus to the list for keyboard navigation
            CategoryListBox.Focus();
            
            // Handle Enter key to confirm selection
            KeyDown += CategorySelectionDialog_KeyDown;
        }

        private void CategoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (CategoryListBox.SelectedItem != null)
            {
                OKButton_Click(sender, e);
            }
        }

        private void CategorySelectionDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && CategoryListBox.SelectedItem != null)
            {
                OKButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
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