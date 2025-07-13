using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace chronos_screentime.Models
{
    public class ChartDataPoint : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private double _value;
        private string _formattedValue = string.Empty;
        private double _percentage;
        private Brush _color = Brushes.Gray;
        private string _category = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
                UpdateFormattedValue();
            }
        }

        public string FormattedValue
        {
            get => _formattedValue;
            set
            {
                _formattedValue = value;
                OnPropertyChanged(nameof(FormattedValue));
            }
        }

        public double Percentage
        {
            get => _percentage;
            set
            {
                _percentage = value;
                OnPropertyChanged(nameof(Percentage));
            }
        }

        public Brush Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged(nameof(Color));
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateFormattedValue()
        {
            var hours = (int)(_value / 3600);
            var minutes = (int)((_value % 3600) / 60);
            
            if (hours > 0)
            {
                FormattedValue = $"{hours}h {minutes}m";
            }
            else
            {
                FormattedValue = $"{minutes}m";
            }
        }
    }

    public class ChartLegendItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _value = string.Empty;
        private Brush _color = Brushes.Gray;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public Brush Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged(nameof(Color));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ChartConfiguration
    {
        public string TimePeriod { get; set; } = "Today";
        public string Granularity { get; set; } = "Daily";
        public string ChartType { get; set; } = "Pie";
        public string DataType { get; set; } = "Category";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public static class ChartColors
    {
        private static readonly List<Brush> _colors = new()
        {
            new SolidColorBrush(Color.FromRgb(52, 152, 219)),   // Blue
            new SolidColorBrush(Color.FromRgb(231, 76, 60)),    // Red
            new SolidColorBrush(Color.FromRgb(46, 204, 113)),   // Green
            new SolidColorBrush(Color.FromRgb(155, 89, 182)),   // Purple
            new SolidColorBrush(Color.FromRgb(241, 196, 15)),   // Yellow
            new SolidColorBrush(Color.FromRgb(230, 126, 34)),   // Orange
            new SolidColorBrush(Color.FromRgb(26, 188, 156)),   // Teal
            new SolidColorBrush(Color.FromRgb(149, 165, 166)),  // Gray
            new SolidColorBrush(Color.FromRgb(142, 68, 173)),   // Dark Purple
            new SolidColorBrush(Color.FromRgb(39, 174, 96)),    // Dark Green
            new SolidColorBrush(Color.FromRgb(192, 57, 43)),    // Dark Red
            new SolidColorBrush(Color.FromRgb(41, 128, 185))    // Dark Blue
        };

        public static Brush GetColor(int index)
        {
            return _colors[index % _colors.Count];
        }

        public static void SetColorForCategory(string category, int index)
        {
            // This could be used to maintain consistent colors for categories
        }
    }
} 