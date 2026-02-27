using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ProcessManager.Converters
{
    public class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value is ProcessPriorityClass p)
            {
                if (p == ProcessPriorityClass.High)
                    return Brushes.Orange;

                if (p == ProcessPriorityClass.RealTime)
                    return Brushes.Red;
            }

            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            return Binding.DoNothing; // ОБЯЗАТЕЛЬНО
        }
    }
}