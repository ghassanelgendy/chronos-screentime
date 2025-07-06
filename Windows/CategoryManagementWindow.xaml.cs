using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using chronos_screentime.Models;
using chronos_screentime.Services;

namespace chronos_screentime.Windows
{
    public partial class CategoryManagementWindow : Window
    {
        private readonly CategoryService _categoryService;
        private readonly ScreenTimeService _screenTimeService;
        private readonly ObservableCollection<CategoryViewModel> _categories;
        private readonly ObservableCollection<AppViewModel> _apps;
        private string? _selectedCategory;

        public CategoryManagementWindow(CategoryService categoryService, ScreenTimeService screenTimeService)
        {
            InitializeComponent();
            _categoryService = categoryService;
            _screenTimeService = screenTimeService;
            _categories = new ObservableCollection<CategoryViewModel>();
            _apps = new ObservableCollection<AppViewModel>();

            CategoriesList.ItemsSource = _categories;
            AppsList.ItemsSource = _apps;

            LoadCategories();
            LoadApps();

            // Subscribe to category changes
            _categoryService.CategoriesChanged += OnCategoriesChanged;
        }

        private void LoadCategories()
        {
            _categories.Clear();
            var appCounts = _categoryService.GetAppCountByCategory();

            foreach (var category in _categoryService.GetAllCategories())
            {
                var isCustom = _categoryService.GetCustomCategories().Contains(category);
                var appCount = appCounts.TryGetValue(category, out var count) ? count : 0;

                _categories.Add(new CategoryViewModel
                {
                    Name = category,
                    AppCount = appCount,
                    IsCustom = isCustom
                });
            }
        }

        private void LoadApps()
        {
            _apps.Clear();
            var allApps = _screenTimeService.GetAllApps();

            foreach (var app in allApps)
            {
                _apps.Add(new AppViewModel
                {
                    AppName = app.AppName,
                    Category = app.Category,
                    FormattedTotalTimeShort = app.FormattedTotalTimeShort,
                    IsSelected = false
                });
            }
        }

        private void OnCategoriesChanged(object? sender, EventArgs e)
        {
            LoadCategories();
            LoadApps();
        }

        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CategoryInputDialog("Add New Category", "");
            if (dialog.ShowDialog() == true)
            {
                var categoryName = dialog.CategoryName?.Trim();
                if (!string.IsNullOrEmpty(categoryName))
                {
                    _categoryService.AddCustomCategory(categoryName);
                }
            }
        }

        private void EditCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryName)
            {
                var dialog = new CategoryInputDialog("Edit Category", categoryName);
                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.CategoryName?.Trim();
                    if (!string.IsNullOrEmpty(newName) && newName != categoryName)
                    {
                        _categoryService.RenameCustomCategory(categoryName, newName);
                    }
                }
            }
        }

        private void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryName)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the category '{categoryName}'? All apps in this category will be moved to 'Uncategorized'.",
                    "Delete Category",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _categoryService.RemoveCustomCategory(categoryName);
                }
            }
        }

        private void ResetToDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all categories to defaults? This will clear all custom categories and app assignments.",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _categoryService.ResetToDefaults();
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all categories? This will remove all custom categories and move all apps to 'Uncategorized'.",
                "Clear All Categories",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _categoryService.ClearAllCategories();
            }
        }

        private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriesList.SelectedItem is CategoryViewModel selectedCategory)
            {
                _selectedCategory = selectedCategory.Name;
                LoadAppsForCategory(selectedCategory.Name);
                AppsHeaderText.Text = $"Apps in '{selectedCategory.Name}'";
                MoveToCategoryButton.Visibility = Visibility.Visible;
            }
            else
            {
                _selectedCategory = null;
                _apps.Clear();
                AppsHeaderText.Text = "Select a category to view apps";
                MoveToCategoryButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadAppsForCategory(string categoryName)
        {
            _apps.Clear();
            var appsInCategory = _screenTimeService.GetAppsByCategory(categoryName);

            foreach (var app in appsInCategory)
            {
                _apps.Add(new AppViewModel
                {
                    AppName = app.AppName,
                    Category = app.Category,
                    FormattedTotalTimeShort = app.FormattedTotalTimeShort,
                    IsSelected = false
                });
            }
        }

        private void MoveToCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = _apps.Where(a => a.IsSelected).ToList();
            if (!selectedApps.Any())
            {
                MessageBox.Show("Please select at least one app to move.", "No Apps Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new CategorySelectionDialog(_categoryService.GetAllCategories().ToList(), "Select Target Category");
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedCategory))
            {
                var targetCategory = dialog.SelectedCategory;
                var appNames = selectedApps.Select(a => a.AppName).ToList();

                _categoryService.SetCategoryForApps(appNames, targetCategory);
                _screenTimeService.RefreshAppCategories();

                // Refresh the display
                LoadCategories();
                if (_selectedCategory != null)
                {
                    LoadAppsForCategory(_selectedCategory);
                }
            }
        }

        private void SelectAllAppsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _apps)
            {
                app.IsSelected = true;
            }
        }

        private void DeselectAllAppsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _apps)
            {
                app.IsSelected = false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Categories are automatically saved when changed
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _categoryService.CategoriesChanged -= OnCategoriesChanged;
            base.OnClosed(e);
        }
    }

    public class CategoryViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _appCount;
        private bool _isCustom;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public int AppCount
        {
            get => _appCount;
            set
            {
                _appCount = value;
                OnPropertyChanged(nameof(AppCount));
            }
        }

        public bool IsCustom
        {
            get => _isCustom;
            set
            {
                _isCustom = value;
                OnPropertyChanged(nameof(IsCustom));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AppViewModel : INotifyPropertyChanged
    {
        private string _appName = string.Empty;
        private string _category = string.Empty;
        private string _formattedTotalTimeShort = string.Empty;
        private bool _isSelected;

        public string AppName
        {
            get => _appName;
            set
            {
                _appName = value;
                OnPropertyChanged(nameof(AppName));
            }
        }

        public string Category
        {
            get => _category;
            set
            {
                _category = value;
                OnPropertyChanged(nameof(Category));
            }
        }

        public string FormattedTotalTimeShort
        {
            get => _formattedTotalTimeShort;
            set
            {
                _formattedTotalTimeShort = value;
                OnPropertyChanged(nameof(FormattedTotalTimeShort));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 