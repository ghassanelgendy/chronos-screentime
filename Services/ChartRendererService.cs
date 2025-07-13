using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using chronos_screentime.Models;

namespace chronos_screentime.Services
{
    public class ChartRendererService
    {
        public void RenderBarChart(Canvas canvas, ObservableCollection<ChartDataPoint> dataPoints)
        {
            canvas.Children.Clear();

            if (!dataPoints.Any())
            {
                // Show "No Data" message
                var noDataText = new TextBlock
                {
                    Text = "No data available for the selected time period",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(noDataText, canvas.ActualWidth / 2 - 100);
                Canvas.SetTop(noDataText, canvas.ActualHeight / 2 - 10);
                canvas.Children.Add(noDataText);
                return;
            }

            // Wait for canvas to be properly sized
            canvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var maxValue = dataPoints.Max(dp => dp.Value);
                    var canvasWidth = canvas.ActualWidth - 100; // Leave margin for labels
                    var canvasHeight = canvas.ActualHeight - 80; // Leave margin for labels
                    var barWidth = canvasWidth / dataPoints.Count * 0.8;
                    var barSpacing = canvasWidth / dataPoints.Count * 0.2;
                    var startX = 50; // Left margin for labels
                    var animationDelay = 0.0;

                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        var dataPoint = dataPoints[i];
                        var barHeight = maxValue > 0 ? (dataPoint.Value / maxValue) * canvasHeight : 0;
                        var x = startX + (i * (barWidth + barSpacing));
                        var y = canvas.ActualHeight - 60 - barHeight; // Bottom margin for labels

                        var barBottom = canvas.ActualHeight - 60;
                        var bar = new Rectangle
                        {
                            Width = barWidth,
                            Height = 0, // Start with 0 height for animation
                            Fill = dataPoint.Color,
                            Stroke = new SolidColorBrush(Colors.White),
                            StrokeThickness = 2,
                            Opacity = 0 // Start invisible for animation
                        };
                        Canvas.SetLeft(bar, x);
                        Canvas.SetTop(bar, barBottom); // Start at the bottom
                        canvas.Children.Add(bar);

                        // Animate bar height
                        var heightAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = barHeight,
                            Duration = TimeSpan.FromMilliseconds(600),
                            BeginTime = TimeSpan.FromMilliseconds(animationDelay),
                            EasingFunction = new System.Windows.Media.Animation.BounceEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Bounces = 2 }
                        };
                        bar.BeginAnimation(Rectangle.HeightProperty, heightAnimation);

                        // Animate bar top (so bar grows upward)
                        var topAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = barBottom,
                            To = y,
                            Duration = TimeSpan.FromMilliseconds(600),
                            BeginTime = TimeSpan.FromMilliseconds(animationDelay),
                            EasingFunction = new System.Windows.Media.Animation.BounceEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Bounces = 2 }
                        };
                        bar.BeginAnimation(Canvas.TopProperty, topAnimation);

                        // Animate opacity
                        var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(300),
                            BeginTime = TimeSpan.FromMilliseconds(animationDelay)
                        };
                        bar.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);

                        // Draw value label on top of bar
                        if (barHeight > 20)
                        {
                            var valueLabel = new TextBlock
                            {
                                Text = dataPoint.FormattedValue,
                                FontSize = 10,
                                FontWeight = FontWeights.Bold,
                                Foreground = new SolidColorBrush(Colors.Black),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Opacity = 0
                            };
                            Canvas.SetLeft(valueLabel, x);
                            Canvas.SetTop(valueLabel, y - 20);
                            canvas.Children.Add(valueLabel);

                            // Animate label
                            var labelOpacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = 0,
                                To = 1,
                                Duration = TimeSpan.FromMilliseconds(300),
                                BeginTime = TimeSpan.FromMilliseconds(animationDelay + 400)
                            };
                            valueLabel.BeginAnimation(UIElement.OpacityProperty, labelOpacityAnimation);
                        }

                        // Draw category label below bar
                        var categoryLabel = new TextBlock
                        {
                            Text = dataPoint.Name,
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Colors.Black),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = barWidth,
                            Opacity = 0
                        };
                        Canvas.SetLeft(categoryLabel, x);
                        Canvas.SetTop(categoryLabel, canvas.ActualHeight - 50);
                        canvas.Children.Add(categoryLabel);

                        // Animate category label
                        var categoryOpacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(300),
                            BeginTime = TimeSpan.FromMilliseconds(animationDelay + 500)
                        };
                        categoryLabel.BeginAnimation(UIElement.OpacityProperty, categoryOpacityAnimation);

                        animationDelay += 100; // Stagger animations
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error rendering bar chart: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void RenderLineChart(Canvas canvas, ObservableCollection<ChartDataPoint> dataPoints)
        {
            canvas.Children.Clear();

            if (!dataPoints.Any())
            {
                // Show "No Data" message
                var noDataText = new TextBlock
                {
                    Text = "No data available for the selected time period",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(noDataText, canvas.ActualWidth / 2 - 100);
                Canvas.SetTop(noDataText, canvas.ActualHeight / 2 - 10);
                canvas.Children.Add(noDataText);
                return;
            }

            // Wait for canvas to be properly sized
            canvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var maxValue = dataPoints.Max(dp => dp.Value);
                    var canvasWidth = canvas.ActualWidth - 100;
                    var canvasHeight = canvas.ActualHeight - 80;
                    var pointSpacing = canvasWidth / (dataPoints.Count - 1);
                    var startX = 50;

                    var points = new PointCollection();
                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        var dataPoint = dataPoints[i];
                        var x = startX + (i * pointSpacing);
                        var y = canvas.ActualHeight - 60 - ((dataPoint.Value / maxValue) * canvasHeight);
                        points.Add(new Point(x, y));
                    }

                    // Draw animated line
                    var polyline = new Polyline
                    {
                        Points = points,
                        Stroke = new SolidColorBrush(Colors.Blue),
                        StrokeThickness = 3,
                        Fill = Brushes.Transparent,
                        Opacity = 0
                    };
                    canvas.Children.Add(polyline);

                    // Animate line
                    var lineOpacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(800),
                        EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    polyline.BeginAnimation(UIElement.OpacityProperty, lineOpacityAnimation);

                    // Draw animated points
                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        var dataPoint = dataPoints[i];
                        var x = startX + (i * pointSpacing);
                        var y = canvas.ActualHeight - 60 - ((dataPoint.Value / maxValue) * canvasHeight);

                        var point = new Ellipse
                        {
                            Width = 0, // Start with 0 size for animation
                            Height = 0,
                            Fill = dataPoint.Color,
                            Stroke = new SolidColorBrush(Colors.White),
                            StrokeThickness = 2,
                            Opacity = 0
                        };
                        Canvas.SetLeft(point, x);
                        Canvas.SetTop(point, y);
                        canvas.Children.Add(point);

                        // Animate point size and opacity
                        var pointSizeAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = 8,
                            Duration = TimeSpan.FromMilliseconds(400),
                            BeginTime = TimeSpan.FromMilliseconds(i * 150),
                            EasingFunction = new System.Windows.Media.Animation.ElasticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Oscillations = 1 }
                        };

                        var pointOpacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(300),
                            BeginTime = TimeSpan.FromMilliseconds(i * 150)
                        };

                        point.BeginAnimation(Ellipse.WidthProperty, pointSizeAnimation);
                        point.BeginAnimation(Ellipse.HeightProperty, pointSizeAnimation);
                        point.BeginAnimation(UIElement.OpacityProperty, pointOpacityAnimation);

                        // Draw value label
                        var valueLabel = new TextBlock
                        {
                            Text = dataPoint.FormattedValue,
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Colors.Black),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Opacity = 0
                        };
                        Canvas.SetLeft(valueLabel, x - 20);
                        Canvas.SetTop(valueLabel, y - 25);
                        canvas.Children.Add(valueLabel);

                        // Animate value label
                        var labelOpacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(300),
                            BeginTime = TimeSpan.FromMilliseconds(i * 150 + 200)
                        };
                        valueLabel.BeginAnimation(UIElement.OpacityProperty, labelOpacityAnimation);

                        // Draw category label
                        var categoryLabel = new TextBlock
                        {
                            Text = dataPoint.Name,
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Colors.Black),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Opacity = 0
                        };
                        Canvas.SetLeft(categoryLabel, x - 20);
                        Canvas.SetTop(categoryLabel, canvas.ActualHeight - 50);
                        canvas.Children.Add(categoryLabel);

                        // Animate category label
                        var categoryOpacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(300),
                            BeginTime = TimeSpan.FromMilliseconds(i * 150 + 300)
                        };
                        categoryLabel.BeginAnimation(UIElement.OpacityProperty, categoryOpacityAnimation);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error rendering line chart: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
} 