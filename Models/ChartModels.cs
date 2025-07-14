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
            new SolidColorBrush(Color.FromRgb(255, 182, 193)),   // Light Pink
            new SolidColorBrush(Color.FromRgb(173, 216, 230)),   // Light Blue
            new SolidColorBrush(Color.FromRgb(144, 238, 144)),   // Light Green
            new SolidColorBrush(Color.FromRgb(221, 160, 221)),   // Plum
            new SolidColorBrush(Color.FromRgb(255, 218, 185)),   // Peach Puff
            new SolidColorBrush(Color.FromRgb(176, 224, 230)),   // Powder Blue
            new SolidColorBrush(Color.FromRgb(255, 228, 196)),   // Bisque
            new SolidColorBrush(Color.FromRgb(230, 230, 250)),   // Lavender
            new SolidColorBrush(Color.FromRgb(240, 248, 255)),   // Alice Blue
            new SolidColorBrush(Color.FromRgb(255, 240, 245)),   // Lavender Blush
            new SolidColorBrush(Color.FromRgb(245, 245, 220)),   // Beige
            new SolidColorBrush(Color.FromRgb(240, 255, 240))    // Honeydew
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