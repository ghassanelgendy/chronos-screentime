using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using chronos_screentime.Models;

namespace chronos_screentime.Services
{
    public class ChartRendererService
    {
        // Zoom state for line chart
        private double _lineChartZoomLevel = 1.0;
        private Point _lineChartZoomCenter = new Point(0, 0);
        private bool _isLineChartZoomEnabled = false;

        public void RenderBarChart(Canvas canvas, ObservableCollection<ChartDataPoint> dataPoints, bool enableAnimations = true)
        {
            canvas.Children.Clear();

            if (!dataPoints.Any())
            {
                // Show "No Data" message
                var noDataText = new TextBlock
                {
                    Text = "No data available for the selected time period",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)), // Off-white
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
                    var canvasPadding = 80; // Increased padding
                    var canvasWidth = canvas.ActualWidth - (canvasPadding * 2); // More padding on sides
                    var canvasHeight = canvas.ActualHeight - (canvasPadding * 2); // More padding top/bottom
                    var barWidth = canvasWidth / dataPoints.Count * 0.8;
                    var barSpacing = canvasWidth / dataPoints.Count * 0.2;
                    var startX = canvasPadding; // Start from padding
                    var animationDelay = 0.0;

                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        var dataPoint = dataPoints[i];
                        var barHeight = maxValue > 0 ? (dataPoint.Value / maxValue) * canvasHeight : 0;
                        var x = startX + (i * (barWidth + barSpacing));
                        var y = canvas.ActualHeight - canvasPadding - barHeight; // Bottom margin for labels

                        var barBottom = canvas.ActualHeight - canvasPadding;
                        
                        // Create curved bar using Path instead of Rectangle
                        var barPath = new Path
                        {
                            Fill = dataPoint.Color,
                            Stroke = null, // No border
                            Opacity = enableAnimations ? 0 : 1 // Start invisible for animation only if enabled
                        };

                        // Create rounded rectangle geometry
                        var radius = Math.Min(barWidth / 4, 8); // Corner radius
                        var rect = new Rect(0, 0, barWidth, 0); // Start with 0 height
                        var geometry = new RectangleGeometry(rect, radius, radius);
                        barPath.Data = geometry;

                        Canvas.SetLeft(barPath, x);
                        Canvas.SetTop(barPath, barBottom); // Start at the bottom
                        canvas.Children.Add(barPath);

                        if (enableAnimations)
                        {
                            // Animate bar height by updating the geometry
                            var heightAnimation = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = 0,
                                To = barHeight,
                                Duration = TimeSpan.FromMilliseconds(600),
                                BeginTime = TimeSpan.FromMilliseconds(animationDelay),
                                EasingFunction = new System.Windows.Media.Animation.BounceEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Bounces = 2 }
                            };

                            // Create animation for the geometry
                            var geometryAnimation = new System.Windows.Media.Animation.RectAnimation
                            {
                                From = new Rect(0, 0, barWidth, 0),
                                To = new Rect(0, 0, barWidth, barHeight),
                                Duration = TimeSpan.FromMilliseconds(600),
                                BeginTime = TimeSpan.FromMilliseconds(animationDelay),
                                EasingFunction = new System.Windows.Media.Animation.BounceEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Bounces = 2 }
                            };

                            // Animate the geometry
                            geometry.BeginAnimation(RectangleGeometry.RectProperty, geometryAnimation);

                            // Animate bar top (so bar grows upward)
                            var topAnimation = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = barBottom,
                                To = y,
                                Duration = TimeSpan.FromMilliseconds(600),
                                BeginTime = TimeSpan.FromMilliseconds(animationDelay),
                                EasingFunction = new System.Windows.Media.Animation.BounceEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Bounces = 2 }
                            };
                            barPath.BeginAnimation(Canvas.TopProperty, topAnimation);

                            // Animate opacity
                            var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = 0,
                                To = 1,
                                Duration = TimeSpan.FromMilliseconds(300),
                                BeginTime = TimeSpan.FromMilliseconds(animationDelay)
                            };
                            barPath.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
                        }
                        else
                        {
                            // Set final position and opacity immediately without animation
                            var finalGeometry = new RectangleGeometry(new Rect(0, 0, barWidth, barHeight), radius, radius);
                            barPath.Data = finalGeometry;
                            Canvas.SetTop(barPath, y);
                        }

                        // Draw value label on top of bar
                        if (barHeight > 20)
                        {
                            var valueLabel = new TextBlock
                            {
                                Text = dataPoint.FormattedValue,
                                FontSize = 10,
                                FontWeight = FontWeights.Bold,
                                Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)), // Off-white
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Opacity = enableAnimations ? 0 : 1
                            };
                            Canvas.SetLeft(valueLabel, x);
                            Canvas.SetTop(valueLabel, y - 20);
                            canvas.Children.Add(valueLabel);

                            if (enableAnimations)
                            {
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
                        }

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
            
            // Reset zoom state
            _lineChartZoomLevel = 1.0;
            _lineChartZoomCenter = new Point(0, 0);
            _isLineChartZoomEnabled = false;

            if (!dataPoints.Any())
            {
                // Show "No Data" message
                var noDataText = new TextBlock
                {
                    Text = "No data available for the selected time period",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)), // Off-white
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(noDataText, canvas.ActualWidth / 2 - 100);
                Canvas.SetTop(noDataText, canvas.ActualHeight / 2 - 10);
                canvas.Children.Add(noDataText);
                return;
            }

            // Add mouse wheel event handler for zoom
            canvas.MouseWheel += (sender, e) => HandleLineChartZoom(canvas, dataPoints, e);
            
            // Add mouse enter/leave handlers to show/hide zoom cursor
            canvas.MouseEnter += (sender, e) => 
            {
                canvas.Cursor = Cursors.Cross;
                _isLineChartZoomEnabled = true;
            };
            canvas.MouseLeave += (sender, e) => 
            {
                canvas.Cursor = Cursors.Arrow;
                _isLineChartZoomEnabled = false;
            };

            // Wait for canvas to be properly sized
            canvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    RenderLineChartContent(canvas, dataPoints);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error rendering line chart: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RenderLineChartContent(Canvas canvas, ObservableCollection<ChartDataPoint> dataPoints)
        {
            var maxValue = dataPoints.Max(dp => dp.Value);
            var canvasPadding = 80; // Increased padding to match bar chart
            var canvasWidth = canvas.ActualWidth - (canvasPadding * 2);
            var canvasHeight = canvas.ActualHeight - (canvasPadding * 2);
            var pointSpacing = canvasWidth / (dataPoints.Count - 1);
            var startX = canvasPadding;

            var points = new PointCollection();
            for (int i = 0; i < dataPoints.Count; i++)
            {
                var dataPoint = dataPoints[i];
                var x = startX + (i * pointSpacing);
                var y = canvas.ActualHeight - canvasPadding - ((dataPoint.Value / maxValue) * canvasHeight);
                points.Add(new Point(x, y));
            }

            // Apply zoom transformation
            var transformGroup = new TransformGroup();
            var scaleTransform = new ScaleTransform(_lineChartZoomLevel, _lineChartZoomLevel);
            var translateTransform = new TranslateTransform(_lineChartZoomCenter.X, _lineChartZoomCenter.Y);
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            canvas.RenderTransform = transformGroup;

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
                var y = canvas.ActualHeight - canvasPadding - ((dataPoint.Value / maxValue) * canvasHeight);

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
                    Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)), // Off-white
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
            }
        }

        private void HandleLineChartZoom(Canvas canvas, ObservableCollection<ChartDataPoint> dataPoints, MouseWheelEventArgs e)
        {
            if (!_isLineChartZoomEnabled) return;

            try
            {
                // Mark the event as handled to prevent it from bubbling up to parent ScrollViewer
                e.Handled = true;
                
                // Get mouse position relative to canvas
                var mousePos = e.GetPosition(canvas);
                
                // Calculate zoom factor (positive delta = zoom in, negative = zoom out)
                var zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
                var newZoomLevel = _lineChartZoomLevel * zoomFactor;
                
                // Limit zoom levels (between 0.5x and 5x)
                newZoomLevel = Math.Max(0.5, Math.Min(5.0, newZoomLevel));
                
                if (Math.Abs(newZoomLevel - _lineChartZoomLevel) < 0.01) return; // Prevent tiny changes
                
                // Calculate zoom center (mouse position)
                var canvasCenter = new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2);
                var zoomCenter = new Point(
                    (mousePos.X - canvasCenter.X) * (1 - zoomFactor),
                    (mousePos.Y - canvasCenter.Y) * (1 - zoomFactor)
                );
                
                // Update zoom state
                _lineChartZoomLevel = newZoomLevel;
                _lineChartZoomCenter = new Point(
                    _lineChartZoomCenter.X + zoomCenter.X,
                    _lineChartZoomCenter.Y + zoomCenter.Y
                );
                
                // Re-render the chart with new zoom
                canvas.Children.Clear();
                RenderLineChartContent(canvas, dataPoints);
                
                System.Diagnostics.Debug.WriteLine($"Line chart zoom: {_lineChartZoomLevel:F2}x at ({_lineChartZoomCenter.X:F1}, {_lineChartZoomCenter.Y:F1})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling line chart zoom: {ex.Message}");
            }
        }


    }
} 