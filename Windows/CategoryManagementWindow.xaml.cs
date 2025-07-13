using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

            // Check if categories exist and auto-categorize if none found
            var allApps = _screenTimeService.GetAllApps().Select(a => a.AppName);
            var allWebsiteDomains = _screenTimeService.GetAllWebsites().Select(w => w.Domain);
            _categoryService.AutoCategorizeIfNoCategoriesExist(allApps, allWebsiteDomains);

            // Also categorize any existing uncategorized items
            _categoryService.CategorizeExistingUncategorizedItems(allApps, allWebsiteDomains);

            LoadCategories();
            LoadApps();

            // Subscribe to category changes
            _categoryService.CategoriesChanged += OnCategoriesChanged;
        }

        private void LoadCategories()
        {
            _categories.Clear();
            var appCounts = _categoryService.GetAppCountByCategory();
            var websiteCounts = _categoryService.GetWebsiteCountByCategory();

            foreach (var category in _categoryService.GetAllCategories())
            {
                var isCustom = _categoryService.GetCustomCategories().Contains(category);
                var appCount = appCounts.TryGetValue(category, out var appCountValue) ? appCountValue : 0;
                var websiteCount = websiteCounts.TryGetValue(category, out var websiteCountValue) ? websiteCountValue : 0;

                _categories.Add(new CategoryViewModel
                {
                    Name = category,
                    AppCount = appCount,
                    WebsiteCount = websiteCount,
                    IsCustom = isCustom
                });
            }
        }

        private void LoadApps()
        {
            _apps.Clear();
            var allApps = _screenTimeService.GetAllApps();
            var allWebsites = _screenTimeService.GetAllWebsites();

            foreach (var app in allApps)
            {
                _apps.Add(new AppViewModel
                {
                    Name = app.AppName,
                    Type = "App",
                    Category = app.Category,
                    FormattedTotalTimeShort = app.FormattedTotalTimeShort,
                    IsSelected = false,
                    ProcessPath = app.ProcessPath
                });
            }

            foreach (var website in allWebsites)
            {
                _apps.Add(new AppViewModel
                {
                    Name = website.DisplayName,
                    Type = "Website",
                    Category = website.Category,
                    FormattedTotalTimeShort = website.FormattedTotalTimeShort,
                    IsSelected = false,
                    ProcessPath = string.Empty, // Websites don't have process paths
                    Domain = website.Domain // Set the Domain property for websites
                });
            }
            
            // Update header text with counts
            var appCount = allApps.Count();
            var websiteCount = allWebsites.Count();
            AppsHeaderText.Text = $"All Apps & Websites ({appCount} apps, {websiteCount} websites)";
            
            // Subscribe to property changes to update button visibility
            foreach (var app in _apps)
            {
                app.PropertyChanged += App_PropertyChanged;
            }
        }

        private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppViewModel.IsSelected))
            {
                UpdateTransferButtonVisibility();
            }
        }

        private void UpdateTransferButtonVisibility()
        {
            var selectedApps = _apps.Where(a => a.IsSelected).ToList();
            TransferToCategoryButton.Visibility = selectedApps.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnCategoriesChanged(object? sender, EventArgs e)
        {
            LoadCategories();
            LoadApps();
        }

        private void InitializeTemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will initialize all categories with their template apps and websites. This action will overwrite any existing category assignments. Continue?",
                    "Initialize with Templates",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _categoryService.InitializeWithTemplates();
                    _screenTimeService.RefreshAppCategories();
                    _screenTimeService.RefreshWebsiteCategories();
                    
                    LoadCategories();
                    LoadApps();
                    
                    MessageBox.Show(
                        "Categories have been initialized with template apps and websites!",
                        "Templates Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AutoCategorizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will automatically categorize all uncategorized apps and websites based on their names/domains. Continue?",
                    "Auto-Categorize Apps & Websites",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var allAppNames = _screenTimeService.GetAllApps().Select(a => a.AppName);
                    var allWebsiteDomains = _screenTimeService.GetAllWebsites().Select(w => w.Domain);
                    
                    _categoryService.AutoCategorizeAllApps(allAppNames);
                    _categoryService.AutoCategorizeAllWebsites(allWebsiteDomains);
                    
                    _screenTimeService.RefreshAppCategories();
                    _screenTimeService.RefreshWebsiteCategories();
                    
                    LoadCategories();
                    LoadApps();
                    
                    MessageBox.Show(
                        "Apps and websites have been automatically categorized!",
                        "Auto-Categorization Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error auto-categorizing apps and websites: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CategorizeExistingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var uncategorizedApps = _categoryService.GetUncategorizedAppCount();
                var uncategorizedWebsites = _categoryService.GetUncategorizedWebsiteCount();
                
                if (uncategorizedApps == 0 && uncategorizedWebsites == 0)
                {
                    MessageBox.Show(
                        "All apps and websites are already categorized!",
                        "No Uncategorized Items",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"This will categorize {uncategorizedApps} uncategorized apps and {uncategorizedWebsites} uncategorized websites based on their names/domains. Continue?",
                    "Categorize Existing Items",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var allApps = _screenTimeService.GetAllApps().Select(a => a.AppName);
                    var allWebsiteDomains = _screenTimeService.GetAllWebsites().Select(w => w.Domain);
                    
                    _categoryService.CategorizeExistingUncategorizedItems(allApps, allWebsiteDomains);
                    
                    _screenTimeService.RefreshAppCategories();
                    _screenTimeService.RefreshWebsiteCategories();
                    
                    LoadCategories();
                    LoadApps();
                    
                    MessageBox.Show(
                        $"Successfully categorized {uncategorizedApps} apps and {uncategorizedWebsites} websites!",
                        "Categorization Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error categorizing existing items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowTemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var templateInfo = new System.Text.StringBuilder();
                templateInfo.AppendLine("Category Templates:");
                templateInfo.AppendLine();

                foreach (var category in _categoryService.GetDefaultCategories())
                {
                    if (category != "Uncategorized")
                    {
                        var appTemplates = _categoryService.GetTemplateAppsForCategory(category);
                        var websiteTemplates = _categoryService.GetTemplateWebsitesForCategory(category);
                        
                        templateInfo.AppendLine($"{category}:");
                        templateInfo.AppendLine($"Apps ({appTemplates.Count}): {string.Join(", ", appTemplates.Take(10))}");
                        if (appTemplates.Count > 10)
                        {
                            templateInfo.AppendLine($"... and {appTemplates.Count - 10} more apps");
                        }
                        
                        templateInfo.AppendLine($"Websites ({websiteTemplates.Count}): {string.Join(", ", websiteTemplates.Take(10))}");
                        if (websiteTemplates.Count > 10)
                        {
                            templateInfo.AppendLine($"... and {websiteTemplates.Count - 10} more websites");
                        }
                        templateInfo.AppendLine();
                    }
                }

                var templateWindow = new TemplateViewerWindow(templateInfo.ToString());
                templateWindow.Owner = this;
                templateWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAllAppsButton_Click(object sender, RoutedEventArgs e)
        {
            // This button is no longer needed since we always show all apps
            // But we can use it to refresh the list
            LoadApps();
            CategoriesList.SelectedItem = null;
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
            // No longer filter apps by category - we always show all apps
            // Just update the selected category for reference
            if (CategoriesList.SelectedItem is CategoryViewModel selectedCategory)
            {
                _selectedCategory = selectedCategory.Name;
            }
            else
            {
                _selectedCategory = null;
            }
        }

        private void TransferToCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _apps.Where(a => a.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select at least one item to transfer.", "No Items Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Show category selection dialog
            var categories = _categoryService.GetAllCategories().ToList();
            var dialog = new CategorySelectionDialog(categories, "Select Target Category");
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedCategory))
            {
                var targetCategory = dialog.SelectedCategory;
                
                // Separate apps and websites
                var selectedApps = selectedItems.Where(item => item.Type == "App").Select(a => a.Name).ToList();
                var selectedWebsites = selectedItems.Where(item => item.Type == "Website").Select(w => w.Domain).ToList();

                try
                {
                    // Transfer apps
                    if (selectedApps.Any())
                    {
                        _categoryService.SetCategoryForApps(selectedApps, targetCategory);
                        _screenTimeService.RefreshAppCategories();
                    }

                    // Transfer websites
                    if (selectedWebsites.Any())
                    {
                        _categoryService.SetCategoryForWebsites(selectedWebsites, targetCategory);
                        _screenTimeService.RefreshWebsiteCategories();
                    }

                    // Show success message
                    var totalItems = selectedApps.Count + selectedWebsites.Count;
                    var message = $"Successfully moved {totalItems} item(s) to '{targetCategory}' category.";
                    if (selectedApps.Any() && selectedWebsites.Any())
                    {
                        message += $"\n• {selectedApps.Count} app(s)\n• {selectedWebsites.Count} website(s)";
                    }
                    else if (selectedApps.Any())
                    {
                        message += $"\n• {selectedApps.Count} app(s)";
                    }
                    else if (selectedWebsites.Any())
                    {
                        message += $"\n• {selectedWebsites.Count} website(s)";
                    }
                    
                    MessageBox.Show(message, "Items Transferred", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh the display
                    LoadCategories();
                    LoadApps();
                    
                    // Clear selection
                    foreach (var item in _apps)
                    {
                        item.IsSelected = false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error transferring items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectAllItemsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _apps)
            {
                item.IsSelected = true;
            }
        }

        private void DeselectAllItemsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _apps)
            {
                item.IsSelected = false;
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
            // Unsubscribe from events
            _categoryService.CategoriesChanged -= OnCategoriesChanged;
            base.OnClosed(e);
        }
    }

    public class CategoryViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _appCount;
        private int _websiteCount;
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

        public int WebsiteCount
        {
            get => _websiteCount;
            set
            {
                _websiteCount = value;
                OnPropertyChanged(nameof(WebsiteCount));
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
        private string _name = string.Empty;
        private string _type = string.Empty;
        private string _category = string.Empty;
        private string _formattedTotalTimeShort = string.Empty;
        private bool _isSelected;
        private string _processPath = string.Empty;
        private string _domain = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
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

        public string ProcessPath
        {
            get => _processPath;
            set
            {
                _processPath = value;
                OnPropertyChanged(nameof(ProcessPath));
            }
        }

        public string Domain
        {
            get => _domain;
            set
            {
                _domain = value;
                OnPropertyChanged(nameof(Domain));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AppWebsiteCountConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is int appCount && values[1] is int websiteCount)
            {
                var parts = new List<string>();
                
                if (appCount > 0)
                    parts.Add($"{appCount} apps");
                
                if (websiteCount > 0)
                    parts.Add($"{websiteCount} websites");
                
                if (parts.Count == 0)
                    return "0 items";
                
                return string.Join(", ", parts);
            }
            
            return "0 items";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 