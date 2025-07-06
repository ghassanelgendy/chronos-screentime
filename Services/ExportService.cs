using chronos_screentime.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chronos_screentime.Services
{
    public class ExportService
    {
        private readonly ScreenTimeService _screenTimeService;

        public ExportService(ScreenTimeService screenTimeService)
        {
            _screenTimeService = screenTimeService;
        }

        public async Task<bool> ExportAppsToCSVAsync(string? filePath = null)
        {
            try
            {
                // If no file path provided, show save dialog
                if (string.IsNullOrEmpty(filePath))
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Title = "Export App Usage Data",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        DefaultExt = "csv",
                        FileName = $"Chronos_App_Usage_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() != true)
                    {
                        return false; // User cancelled
                    }

                    filePath = saveFileDialog.FileName;
                }

                var apps = _screenTimeService.GetAllApps().ToList();
                
                // Create CSV content
                var csvContent = new StringBuilder();
                
                // Add header
                csvContent.AppendLine("App Name,Total Time (Hours),Total Time (Minutes),Total Sessions,First Seen,Last Seen,Process Path");
                
                // Add data rows
                foreach (var app in apps.OrderByDescending(a => a.TotalTime))
                {
                    var totalHours = app.TotalTime.TotalHours;
                    var totalMinutes = app.TotalTime.TotalMinutes;
                    var firstSeen = app.FirstSeen.ToString("yyyy-MM-dd HH:mm:ss");
                    var lastSeen = app.LastSeen.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    // Escape app name if it contains commas
                    var appName = app.AppName.Contains(",") ? $"\"{app.AppName}\"" : app.AppName;
                    var processPath = app.ProcessPath.Contains(",") ? $"\"{app.ProcessPath}\"" : app.ProcessPath;
                    
                    csvContent.AppendLine($"{appName},{totalHours:F2},{totalMinutes:F2},{app.SessionCount},{firstSeen},{lastSeen},{processPath}");
                }

                // Write to file
                await File.WriteAllTextAsync(filePath, csvContent.ToString(), Encoding.UTF8);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportService: Error exporting apps to CSV: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ExportDetailedAppsToCSVAsync(string? filePath = null)
        {
            try
            {
                // If no file path provided, show save dialog
                if (string.IsNullOrEmpty(filePath))
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Title = "Export Detailed App Usage Data",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        DefaultExt = "csv",
                        FileName = $"Chronos_Detailed_App_Usage_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() != true)
                    {
                        return false; // User cancelled
                    }

                    filePath = saveFileDialog.FileName;
                }

                var apps = _screenTimeService.GetAllApps().ToList();
                
                // Create CSV content
                var csvContent = new StringBuilder();
                
                // Add header
                csvContent.AppendLine("App Name,Date,Time Spent (Hours),Time Spent (Minutes),Sessions,Process Path");
                
                // Add data rows with daily breakdown
                foreach (var app in apps.OrderByDescending(a => a.TotalTime))
                {
                    // Escape app name if it contains commas
                    var appName = app.AppName.Contains(",") ? $"\"{app.AppName}\"" : app.AppName;
                    var processPath = app.ProcessPath.Contains(",") ? $"\"{app.ProcessPath}\"" : app.ProcessPath;
                    
                    // Add daily entries
                    foreach (var dailyEntry in app.DailyTimes.OrderBy(kvp => kvp.Key))
                    {
                        var date = dailyEntry.Key.ToString("yyyy-MM-dd");
                        var timeSpent = dailyEntry.Value;
                        var hours = timeSpent.TotalHours;
                        var minutes = timeSpent.TotalMinutes;
                        var sessions = app.GetSessionsForDate(dailyEntry.Key);
                        
                        csvContent.AppendLine($"{appName},{date},{hours:F2},{minutes:F2},{sessions},{processPath}");
                    }
                }

                // Write to file
                await File.WriteAllTextAsync(filePath, csvContent.ToString(), Encoding.UTF8);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportService: Error exporting detailed apps to CSV: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ExportWebsitesToCSVAsync(string? filePath = null)
        {
            try
            {
                // If no file path provided, show save dialog
                if (string.IsNullOrEmpty(filePath))
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Title = "Export Website Usage Data",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        DefaultExt = "csv",
                        FileName = $"Chronos_Website_Usage_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() != true)
                    {
                        return false; // User cancelled
                    }

                    filePath = saveFileDialog.FileName;
                }

                var websites = _screenTimeService.GetAllWebsites().ToList();
                
                // Create CSV content
                var csvContent = new StringBuilder();
                
                // Add header
                csvContent.AppendLine("Domain,Total Time (Hours),Total Time (Minutes),Total Sessions,First Seen,Last Seen,Favicon URL");
                
                // Add data rows
                foreach (var website in websites.OrderByDescending(w => w.TotalTime))
                {
                    var totalHours = website.TotalTime.TotalHours;
                    var totalMinutes = website.TotalTime.TotalMinutes;
                    var firstSeen = website.FirstSeen.ToString("yyyy-MM-dd HH:mm:ss");
                    var lastSeen = website.LastSeen.ToString("yyyy-MM-dd HH:mm:ss");
                    var faviconUrl = website.FaviconUrl ?? "";
                    
                    // Escape domain if it contains commas
                    var domain = website.Domain.Contains(",") ? $"\"{website.Domain}\"" : website.Domain;
                    var favicon = faviconUrl.Contains(",") ? $"\"{faviconUrl}\"" : faviconUrl;
                    
                    csvContent.AppendLine($"{domain},{totalHours:F2},{totalMinutes:F2},{website.SessionCount},{firstSeen},{lastSeen},{favicon}");
                }

                // Write to file
                await File.WriteAllTextAsync(filePath, csvContent.ToString(), Encoding.UTF8);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportService: Error exporting websites to CSV: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ExportDetailedWebsitesToCSVAsync(string? filePath = null)
        {
            try
            {
                // If no file path provided, show save dialog
                if (string.IsNullOrEmpty(filePath))
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Title = "Export Detailed Website Usage Data",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        DefaultExt = "csv",
                        FileName = $"Chronos_Detailed_Website_Usage_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() != true)
                    {
                        return false; // User cancelled
                    }

                    filePath = saveFileDialog.FileName;
                }

                var websites = _screenTimeService.GetAllWebsites().ToList();
                
                // Create CSV content
                var csvContent = new StringBuilder();
                
                // Add header
                csvContent.AppendLine("Domain,Date,Time Spent (Hours),Time Spent (Minutes),Sessions,Favicon URL");
                
                // Add data rows with daily breakdown
                foreach (var website in websites.OrderByDescending(w => w.TotalTime))
                {
                    // Escape domain if it contains commas
                    var domain = website.Domain.Contains(",") ? $"\"{website.Domain}\"" : website.Domain;
                    var faviconUrl = website.FaviconUrl ?? "";
                    var favicon = faviconUrl.Contains(",") ? $"\"{faviconUrl}\"" : faviconUrl;
                    
                    // Add daily entries
                    foreach (var dailyEntry in website.DailyTimes.OrderBy(kvp => kvp.Key))
                    {
                        var date = dailyEntry.Key.ToString("yyyy-MM-dd");
                        var timeSpent = dailyEntry.Value;
                        var hours = timeSpent.TotalHours;
                        var minutes = timeSpent.TotalMinutes;
                        var sessions = website.GetSessionsForDate(dailyEntry.Key);
                        
                        csvContent.AppendLine($"{domain},{date},{hours:F2},{minutes:F2},{sessions},{favicon}");
                    }
                }

                // Write to file
                await File.WriteAllTextAsync(filePath, csvContent.ToString(), Encoding.UTF8);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportService: Error exporting detailed websites to CSV: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ExportAllDataToCSVAsync(string? filePath = null)
        {
            try
            {
                // If no file path provided, show save dialog
                if (string.IsNullOrEmpty(filePath))
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Title = "Export All Usage Data",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        DefaultExt = "csv",
                        FileName = $"Chronos_All_Usage_Data_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() != true)
                    {
                        return false; // User cancelled
                    }

                    filePath = saveFileDialog.FileName;
                }

                var apps = _screenTimeService.GetAllApps().ToList();
                var websites = _screenTimeService.GetAllWebsites().ToList();
                
                // Create CSV content
                var csvContent = new StringBuilder();
                
                // Add header
                csvContent.AppendLine("Type,Name,Date,Time Spent (Hours),Time Spent (Minutes),Sessions,Additional Info");
                
                // Add app data
                foreach (var app in apps.OrderByDescending(a => a.TotalTime))
                {
                    var appName = app.AppName.Contains(",") ? $"\"{app.AppName}\"" : app.AppName;
                    var processPath = app.ProcessPath.Contains(",") ? $"\"{app.ProcessPath}\"" : app.ProcessPath;
                    
                    foreach (var dailyEntry in app.DailyTimes.OrderBy(kvp => kvp.Key))
                    {
                        var date = dailyEntry.Key.ToString("yyyy-MM-dd");
                        var timeSpent = dailyEntry.Value;
                        var hours = timeSpent.TotalHours;
                        var minutes = timeSpent.TotalMinutes;
                        var sessions = app.GetSessionsForDate(dailyEntry.Key);
                        
                        csvContent.AppendLine($"App,{appName},{date},{hours:F2},{minutes:F2},{sessions},{processPath}");
                    }
                }
                
                // Add website data
                foreach (var website in websites.OrderByDescending(w => w.TotalTime))
                {
                    var domain = website.Domain.Contains(",") ? $"\"{website.Domain}\"" : website.Domain;
                    var faviconUrl = website.FaviconUrl ?? "";
                    var favicon = faviconUrl.Contains(",") ? $"\"{faviconUrl}\"" : faviconUrl;
                    
                    foreach (var dailyEntry in website.DailyTimes.OrderBy(kvp => kvp.Key))
                    {
                        var date = dailyEntry.Key.ToString("yyyy-MM-dd");
                        var timeSpent = dailyEntry.Value;
                        var hours = timeSpent.TotalHours;
                        var minutes = timeSpent.TotalMinutes;
                        var sessions = website.GetSessionsForDate(dailyEntry.Key);
                        
                        csvContent.AppendLine($"Website,{domain},{date},{hours:F2},{minutes:F2},{sessions},{favicon}");
                    }
                }

                // Write to file
                await File.WriteAllTextAsync(filePath, csvContent.ToString(), Encoding.UTF8);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportService: Error exporting all data to CSV: {ex.Message}");
                throw;
            }
        }
    }
} 