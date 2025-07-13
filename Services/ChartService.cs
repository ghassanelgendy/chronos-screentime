using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using chronos_screentime.Models;

namespace chronos_screentime.Services
{
    public class ChartService
    {
        private readonly ScreenTimeService _screenTimeService;
        private readonly CategoryService _categoryService;

        public ChartService(ScreenTimeService screenTimeService, CategoryService categoryService)
        {
            _screenTimeService = screenTimeService;
            _categoryService = categoryService;
        }

        public ChartConfiguration GetChartConfiguration(string timePeriod, string chartType, string dataType)
        {
            var config = new ChartConfiguration
            {
                TimePeriod = timePeriod,
                Granularity = "Daily", // Default granularity
                ChartType = chartType,
                DataType = dataType
            };

            // Set date range based on time period
            var today = DateTime.Today;
            switch (timePeriod)
            {
                case "Today":
                    config.StartDate = today;
                    config.EndDate = today.AddDays(1).AddSeconds(-1);
                    break;
                case "Yesterday":
                    config.StartDate = today.AddDays(-1);
                    config.EndDate = today.AddSeconds(-1);
                    break;
                case "ThisWeek":
                    var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                    config.StartDate = startOfWeek;
                    config.EndDate = startOfWeek.AddDays(7).AddSeconds(-1);
                    break;
                case "LastWeek":
                    var lastWeekStart = today.AddDays(-(int)today.DayOfWeek - 7);
                    config.StartDate = lastWeekStart;
                    config.EndDate = lastWeekStart.AddDays(7).AddSeconds(-1);
                    break;
                case "ThisMonth":
                    config.StartDate = new DateTime(today.Year, today.Month, 1);
                    config.EndDate = config.StartDate.AddMonths(1).AddSeconds(-1);
                    break;
                case "ThisYear":
                    config.StartDate = new DateTime(today.Year, 1, 1);
                    config.EndDate = config.StartDate.AddYears(1).AddSeconds(-1);
                    break;
                default:
                    config.StartDate = today;
                    config.EndDate = today.AddDays(1).AddSeconds(-1);
                    break;
            }

            return config;
        }

        public ObservableCollection<ChartDataPoint> GetChartData(ChartConfiguration config)
        {
            var dataPoints = new ObservableCollection<ChartDataPoint>();

            try
            {
                // Get all apps and websites for the time period
                var allApps = _screenTimeService.GetAllApps();
                var allWebsites = _screenTimeService.GetAllWebsites();

                if (config.DataType == "Category")
                {
                    // Group by category
                    var categoryData = new Dictionary<string, double>();

                    // Process apps based on time period
                    foreach (var app in allApps)
                    {
                        var category = app.Category ?? "Uncategorized";
                        var appTotalTime = GetAppTimeForPeriod(app, config);

                        if (appTotalTime > 0)
                        {
                            if (categoryData.ContainsKey(category))
                                categoryData[category] += appTotalTime;
                            else
                                categoryData[category] = appTotalTime;
                        }
                    }

                    // Process websites based on time period
                    foreach (var website in allWebsites)
                    {
                        var category = website.Category ?? "Uncategorized";
                        var websiteTotalTime = GetWebsiteTimeForPeriod(website, config);

                        if (websiteTotalTime > 0)
                        {
                            if (categoryData.ContainsKey(category))
                                categoryData[category] += websiteTotalTime;
                            else
                                categoryData[category] = websiteTotalTime;
                        }
                    }

                    // Convert to chart data points
                    var totalTimeSeconds = categoryData.Values.Sum();
                    var index = 0;

                    foreach (var kvp in categoryData.OrderByDescending(x => x.Value))
                    {
                        if (kvp.Value > 0) // Only include categories with time
                        {
                            var percentage = totalTimeSeconds > 0 ? (kvp.Value / totalTimeSeconds) * 100 : 0;
                            
                            dataPoints.Add(new ChartDataPoint
                            {
                                Name = kvp.Key,
                                Value = kvp.Value,
                                Percentage = percentage,
                                Color = ChartColors.GetColor(index),
                                Category = kvp.Key
                            });
                            
                            index++;
                        }
                    }
                }
                else // DataType == "Item"
                {
                    // Group by individual apps and websites
                    var itemData = new Dictionary<string, double>();

                    // Process apps based on time period
                    foreach (var app in allApps)
                    {
                        var appTotalTime = GetAppTimeForPeriod(app, config);
                        if (appTotalTime > 0)
                        {
                            itemData[app.AppName] = appTotalTime;
                        }
                    }

                    // Process websites based on time period
                    foreach (var website in allWebsites)
                    {
                        var websiteTotalTime = GetWebsiteTimeForPeriod(website, config);
                        if (websiteTotalTime > 0)
                        {
                            itemData[website.DisplayName] = websiteTotalTime;
                        }
                    }

                    // Get total time for all items
                    var totalTimeSeconds = itemData.Values.Sum();
                    var index = 0;

                    // Take top 20 items for display
                    var topItems = itemData.OrderByDescending(x => x.Value).Take(20).ToList();
                    var topItemsTotal = topItems.Sum(x => x.Value);

                    foreach (var kvp in topItems)
                    {
                        if (kvp.Value > 0) // Only include items with time
                        {
                            var percentage = totalTimeSeconds > 0 ? (kvp.Value / totalTimeSeconds) * 100 : 0;
                            
                            dataPoints.Add(new ChartDataPoint
                            {
                                Name = kvp.Key,
                                Value = kvp.Value,
                                Percentage = percentage,
                                Color = ChartColors.GetColor(index),
                                Category = "Individual Item"
                            });
                            
                            index++;
                        }
                    }

                    // Add 'Other' slice for remaining items if there are more than 20 items
                    if (itemData.Count > 20 && totalTimeSeconds > topItemsTotal)
                    {
                        var otherValue = totalTimeSeconds - topItemsTotal;
                        var otherPercentage = totalTimeSeconds > 0 ? (otherValue / totalTimeSeconds) * 100 : 0;
                        
                        dataPoints.Add(new ChartDataPoint
                        {
                            Name = "Other",
                            Value = otherValue,
                            Percentage = otherPercentage,
                            Color = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray),
                            Category = "Other"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating chart data: {ex.Message}");
            }

            return dataPoints;
        }

        private double GetAppTimeForPeriod(AppScreenTime app, ChartConfiguration config)
        {
            var totalSeconds = 0.0;
            
            switch (config.TimePeriod)
            {
                case "Today":
                    totalSeconds = app.TodaysTime.TotalSeconds;
                    break;
                case "Yesterday":
                    totalSeconds = app.GetTimeForDate(DateTime.Today.AddDays(-1)).TotalSeconds;
                    break;
                case "ThisWeek":
                    var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    totalSeconds = app.GetWeekTotal(startOfWeek).TotalSeconds;
                    break;
                case "LastWeek":
                    var lastWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek - 7);
                    totalSeconds = app.GetWeekTotal(lastWeekStart).TotalSeconds;
                    break;
                case "ThisMonth":
                    totalSeconds = app.GetMonthTotal(DateTime.Today.Year, DateTime.Today.Month).TotalSeconds;
                    break;
                case "ThisYear":
                    totalSeconds = app.GetYearTotal(DateTime.Today.Year).TotalSeconds;
                    break;
                default:
                    totalSeconds = app.TodaysTime.TotalSeconds;
                    break;
            }
            
            return totalSeconds;
        }

        private double GetWebsiteTimeForPeriod(WebsiteScreenTime website, ChartConfiguration config)
        {
            var totalSeconds = 0.0;
            
            switch (config.TimePeriod)
            {
                case "Today":
                    totalSeconds = website.TodaysTime.TotalSeconds;
                    break;
                case "Yesterday":
                    totalSeconds = website.GetTimeForDate(DateTime.Today.AddDays(-1)).TotalSeconds;
                    break;
                case "ThisWeek":
                    var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    totalSeconds = website.GetWeekTotal(startOfWeek).TotalSeconds;
                    break;
                case "LastWeek":
                    var lastWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek - 7);
                    totalSeconds = website.GetWeekTotal(lastWeekStart).TotalSeconds;
                    break;
                case "ThisMonth":
                    totalSeconds = website.GetMonthTotal(DateTime.Today.Year, DateTime.Today.Month).TotalSeconds;
                    break;
                case "ThisYear":
                    totalSeconds = website.GetYearTotal(DateTime.Today.Year).TotalSeconds;
                    break;
                default:
                    totalSeconds = website.TodaysTime.TotalSeconds;
                    break;
            }
            
            return totalSeconds;
        }

        public ObservableCollection<ChartLegendItem> GetChartLegend(ObservableCollection<ChartDataPoint> dataPoints)
        {
            var legendItems = new ObservableCollection<ChartLegendItem>();

            foreach (var dataPoint in dataPoints)
            {
                legendItems.Add(new ChartLegendItem
                {
                    Name = dataPoint.Name,
                    Value = dataPoint.FormattedValue,
                    Color = dataPoint.Color
                });
            }

            return legendItems;
        }

        public string GetChartTitle(ChartConfiguration config)
        {
            var periodText = config.TimePeriod switch
            {
                "Today" => "Today's",
                "Yesterday" => "Yesterday's",
                "ThisWeek" => "This Week's",
                "LastWeek" => "Last Week's",
                "ThisMonth" => "This Month's",
                "ThisYear" => "This Year's",
                _ => "Today's"
            };

            var dataTypeText = config.DataType switch
            {
                "Category" => "by Category",
                "Item" => "by App/Website",
                _ => "by Category"
            };

            return $"{periodText} Screen Time {dataTypeText}";
        }

        public string GetChartSubtitle(ChartConfiguration config)
        {
            var granularityText = config.Granularity switch
            {
                "Daily" => "daily breakdown",
                "Weekly" => "weekly breakdown",
                "Monthly" => "monthly breakdown",
                "Yearly" => "yearly breakdown",
                _ => "daily breakdown"
            };

            return $"Showing {granularityText}";
        }

        public string GetFormattedTotalTime(ObservableCollection<ChartDataPoint> dataPoints)
        {
            var totalSeconds = dataPoints.Sum(dp => dp.Value);
            var hours = (int)(totalSeconds / 3600);
            var minutes = (int)((totalSeconds % 3600) / 60);

            if (hours > 0)
                return $"{hours}h {minutes}m";
            else
                return $"{minutes}m";
        }

        public string GetTopCategory(ObservableCollection<ChartDataPoint> dataPoints)
        {
            var topDataPoint = dataPoints.OrderByDescending(dp => dp.Value).FirstOrDefault();
            return topDataPoint?.Name ?? "None";
        }

        public int GetCategoriesCount(ObservableCollection<ChartDataPoint> dataPoints)
        {
            return dataPoints.Count;
        }
    }
} 