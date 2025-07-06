using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using chronos_screentime.Models;
using Wpf.Ui.Controls;

namespace chronos_screentime.Windows
{
    public partial class SleepDetailsWindow : FluentWindow
    {
        public SleepDetailsWindow(DailySleepSummary summary)
        {
            InitializeComponent();
            LoadSleepData(summary);
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadSleepData(DailySleepSummary summary)
        {
            // Set header
            DateHeader.Text = $"Sleep Details - {summary.Date:dddd, MMMM dd, yyyy}";

            // Set summary stats
            TotalSleepText.Text = summary.FormattedTotalTime;
            SessionCountText.Text = summary.SessionCount.ToString();

            // Calculate efficiency (assuming 8 hours is target)
            var targetMinutes = 8 * 60;
            var actualMinutes = summary.TotalSleepTime.TotalMinutes;
            var efficiency = Math.Min(100, (actualMinutes / targetMinutes) * 100);
            EfficiencyText.Text = $"{efficiency:F0}%";

            // Calculate quality based on session count and gaps
            string quality = "Good";
            var qualityBrush = "SystemFillColorSuccessBrush";
            
            if (summary.SessionCount > 3)
            {
                quality = "Poor";
                qualityBrush = "SystemFillColorCriticalBrush";
            }
            else if (summary.SessionCount > 1)
            {
                quality = "Fair";
                qualityBrush = "SystemFillColorCautionBrush";
            }
            
            QualityText.Text = quality;
            QualityText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, qualityBrush);

            // Create session detail cards
            CreateSessionDetailCards(summary);

            // Draw timeline graph
            DrawSleepTimeline(summary);
        }
        
        private void CreateSessionDetailCards(DailySleepSummary summary)
        {
            SessionDetailsPanel.Children.Clear();
            
            if (summary.Sessions == null || !summary.Sessions.Any())
            {
                var noDataText = new System.Windows.Controls.TextBlock
                {
                    Text = "No sleep sessions found for this day.",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                noDataText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                SessionDetailsPanel.Children.Add(noDataText);
                return;
            }
            
            var sessionColors = new[]
            {
                "AccentFillColorDefaultBrush",
                "SystemFillColorSuccessBrush", 
                "SystemFillColorCautionBrush",
                "SystemFillColorCriticalBrush",
                "AccentFillColorSecondaryBrush"
            };
            
            for (int i = 0; i < summary.Sessions.Count; i++)
            {
                var session = summary.Sessions[i];
                var colorBrush = sessionColors[i % sessionColors.Length];
                
                var sessionCard = new Wpf.Ui.Controls.Card
                {
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                
                var cardGrid = new Grid();
                cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                // Session number and duration
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                var sessionNumber = new System.Windows.Controls.TextBlock
                {
                    Text = $"Session {i + 1}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14
                };
                sessionNumber.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, colorBrush);
                
                var durationText = new System.Windows.Controls.TextBlock
                {
                    Text = session.FormattedDuration,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                durationText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
                
                headerPanel.Children.Add(sessionNumber);
                headerPanel.Children.Add(durationText);
                Grid.SetRow(headerPanel, 0);
                cardGrid.Children.Add(headerPanel);
                
                // Time range
                var timeText = new System.Windows.Controls.TextBlock
                {
                    Text = $"{session.Started:HH:mm} - {session.Ended:HH:mm}",
                    FontSize = 13,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                timeText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                Grid.SetRow(timeText, 1);
                cardGrid.Children.Add(timeText);
                
                // Date (if different from main date)
                if (session.Started.Date != summary.Date)
                {
                    var dateText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"({session.Started:MMM dd})",
                        FontSize = 11,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    dateText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
                    Grid.SetRow(dateText, 2);
                    cardGrid.Children.Add(dateText);
                }
                
                sessionCard.Content = cardGrid;
                SessionDetailsPanel.Children.Add(sessionCard);
            }
        }

        private void DrawSleepTimeline(DailySleepSummary summary)
        {
            TimelineCanvas.Children.Clear();

            if (summary.Sessions == null || !summary.Sessions.Any())
                return;

            // Use actual canvas size when rendered
            this.Loaded += (s, e) => {
                if (TimelineCanvas.ActualWidth > 0)
                {
                    DrawTimelineActual(summary);
                }
            };
        }
        
        private void DrawTimelineActual(DailySleepSummary summary)
        {
            TimelineCanvas.Children.Clear();
            
            if (summary.Sessions == null || !summary.Sessions.Any())
                return;

            // Canvas dimensions
            var canvasWidth = TimelineCanvas.ActualWidth;
            var canvasHeight = TimelineCanvas.ActualHeight;
            var padding = 20.0;

            // 24-hour timeline (from previous day 6 PM to current day 6 PM)
            var timelineStart = summary.Date.AddHours(-6); // 6 PM previous day
            var timelineEnd = summary.Date.AddHours(18);   // 6 PM current day
            var totalHours = 24;

            // Draw background timeline with hour markers
            for (int hour = 0; hour <= 24; hour += 6)
            {
                var x = padding + (hour / (double)totalHours) * (canvasWidth - 2 * padding);
                var line = new Line
                {
                    X1 = x,
                    Y1 = padding,
                    X2 = x,
                    Y2 = canvasHeight - padding,
                    StrokeThickness = 1
                };
                line.SetResourceReference(Shape.StrokeProperty, "ControlStrokeColorDefaultBrush");
                TimelineCanvas.Children.Add(line);
            }

            // Draw sleep sessions
            var sessionColors = new[]
            {
                "AccentFillColorDefaultBrush",
                "SystemFillColorSuccessBrush", 
                "SystemFillColorCautionBrush",
                "SystemFillColorCriticalBrush",
                "AccentFillColorSecondaryBrush"
            };

            for (int i = 0; i < summary.Sessions.Count; i++)
            {
                var session = summary.Sessions[i];
                var colorBrush = sessionColors[i % sessionColors.Length];

                // Convert session times to canvas coordinates
                var sessionStart = session.Started;
                var sessionEnd = session.Ended;

                // Calculate position on timeline
                var startHours = (sessionStart - timelineStart).TotalHours;
                var endHours = (sessionEnd - timelineStart).TotalHours;

                // Skip if outside our 24-hour window
                if (startHours < 0 || startHours > 24) continue;

                var startX = padding + (startHours / totalHours) * (canvasWidth - 2 * padding);
                var width = Math.Max(4, ((endHours - startHours) / totalHours) * (canvasWidth - 2 * padding));

                // Draw sleep session rectangle
                var sessionRect = new Rectangle
                {
                    Width = width,
                    Height = canvasHeight - (2 * padding) - 20,
                    RadiusX = 4,
                    RadiusY = 4,
                    StrokeThickness = 2
                };
                sessionRect.SetResourceReference(Shape.FillProperty, colorBrush);
                sessionRect.SetResourceReference(Shape.StrokeProperty, "ControlStrokeColorDefaultBrush");

                Canvas.SetLeft(sessionRect, startX);
                Canvas.SetTop(sessionRect, padding + 10);
                TimelineCanvas.Children.Add(sessionRect);

                // Add session label
                var sessionLabel = new System.Windows.Controls.TextBlock
                {
                    Text = $"{i + 1}",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                sessionLabel.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");

                Canvas.SetLeft(sessionLabel, startX + width / 2 - 8);
                Canvas.SetTop(sessionLabel, padding + 10 + (canvasHeight - 2 * padding - 20) / 2 - 10);
                TimelineCanvas.Children.Add(sessionLabel);
            }
        }
    }
}
