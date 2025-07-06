using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace chronos_screentime.Services
{
    public class CategoryService
    {
        private readonly string _categoriesFilePath;
        private readonly Dictionary<string, string> _appCategories; // AppName -> Category
        private readonly List<string> _customCategories;
        private readonly List<string> _defaultCategories;

        public event EventHandler? CategoriesChanged;

        public CategoryService()
        {
            _appCategories = new Dictionary<string, string>();
            _customCategories = new List<string>();
            
            // Default categories that come with the app
            _defaultCategories = new List<string>
            {
                "WebBrowsing",
                "Development", 
                "Gaming",
                "Communication",
                "Productivity",
                "Entertainment",
                "Social Media",
                "Education",
                "Utilities",
                "Uncategorized"
            };

            _categoriesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChronosScreenTime",
                "categories.json"
            );

            LoadCategories();
        }

        public IEnumerable<string> GetAllCategories()
        {
            return _defaultCategories.Concat(_customCategories).Distinct();
        }

        public IEnumerable<string> GetDefaultCategories()
        {
            return _defaultCategories.ToList();
        }

        public IEnumerable<string> GetCustomCategories()
        {
            return _customCategories.ToList();
        }

        public string GetCategoryForApp(string appName)
        {
            return _appCategories.TryGetValue(appName, out var category) ? category : "Uncategorized";
        }

        public void SetCategoryForApp(string appName, string category)
        {
            if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(category))
                return;

            _appCategories[appName] = category;
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetCategoryForApps(IEnumerable<string> appNames, string category)
        {
            if (string.IsNullOrEmpty(category))
                return;

            foreach (var appName in appNames)
            {
                if (!string.IsNullOrEmpty(appName))
                {
                    _appCategories[appName] = category;
                }
            }
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddCustomCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName) || 
                _defaultCategories.Contains(categoryName) || 
                _customCategories.Contains(categoryName))
                return;

            _customCategories.Add(categoryName);
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveCustomCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName) || !_customCategories.Contains(categoryName))
                return;

            _customCategories.Remove(categoryName);

            // Move apps from this category to Uncategorized
            var appsToMove = _appCategories.Where(kvp => kvp.Value == categoryName).ToList();
            foreach (var kvp in appsToMove)
            {
                _appCategories[kvp.Key] = "Uncategorized";
            }

            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RenameCustomCategory(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) ||
                !_customCategories.Contains(oldName) ||
                _defaultCategories.Contains(newName) ||
                _customCategories.Contains(newName))
                return;

            _customCategories.Remove(oldName);
            _customCategories.Add(newName);

            // Update all apps that were in the old category
            var appsToUpdate = _appCategories.Where(kvp => kvp.Value == oldName).ToList();
            foreach (var kvp in appsToUpdate)
            {
                _appCategories[kvp.Key] = newName;
            }

            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public Dictionary<string, int> GetAppCountByCategory()
        {
            var result = new Dictionary<string, int>();
            
            foreach (var category in GetAllCategories())
            {
                result[category] = _appCategories.Count(kvp => kvp.Value == category);
            }

            return result;
        }

        public IEnumerable<string> GetAppsInCategory(string category)
        {
            return _appCategories.Where(kvp => kvp.Value == category).Select(kvp => kvp.Key);
        }

        public void ClearAllCategories()
        {
            _appCategories.Clear();
            _customCategories.Clear();
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetToDefaults()
        {
            _appCategories.Clear();
            _customCategories.Clear();
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadCategories()
        {
            try
            {
                if (File.Exists(_categoriesFilePath))
                {
                    var json = File.ReadAllText(_categoriesFilePath);
                    var data = JsonConvert.DeserializeObject<CategoryData>(json);
                    
                    if (data != null)
                    {
                        _appCategories.Clear();
                        foreach (var kvp in data.AppCategories)
                        {
                            _appCategories[kvp.Key] = kvp.Value;
                        }

                        _customCategories.Clear();
                        foreach (var category in data.CustomCategories)
                        {
                            if (!_defaultCategories.Contains(category))
                            {
                                _customCategories.Add(category);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
                // Use defaults if loading fails
            }
        }

        private void SaveCategories()
        {
            try
            {
                var directory = Path.GetDirectoryName(_categoriesFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new CategoryData
                {
                    AppCategories = _appCategories,
                    CustomCategories = _customCategories
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_categoriesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving categories: {ex.Message}");
            }
        }

        private class CategoryData
        {
            public Dictionary<string, string> AppCategories { get; set; } = new();
            public List<string> CustomCategories { get; set; } = new();
        }
    }
} 