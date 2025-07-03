using System;
using System.Globalization;
using System.Windows.Data;

namespace chronos_screentime
{
    public class WrapPanelWidthConverter : IValueConverter
    {
        private const double MinWidthForTwoColumns = 1000; // Minimum width needed to show two columns
        private const double SingleColumnWidth = 600; // Maximum width for a single column
        private const double Margin = 32; // Total horizontal margin (16 on each side)

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double containerWidth)
            {
                // Account for margins
                double availableWidth = containerWidth - Margin;

                // If we have enough space for two columns (with some padding)
                if (availableWidth >= MinWidthForTwoColumns)
                {
                    // Return half the available width (minus a small gap between columns)
                    return Math.Min((availableWidth - 16) / 2, SingleColumnWidth);
                }

                // Otherwise return full width up to maximum
                return Math.Min(availableWidth, SingleColumnWidth);
            }

            return SingleColumnWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 