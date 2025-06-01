using System;
using System.Globalization;
using System.Windows.Data;

namespace Projet.Wpf.View.Converters
{
    // Converter for blocked package items
    public class BlockedPackageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string packageName)
            {
                return new Tuple<string, string>("blockedPackage", packageName);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for crypto extension items
    public class CryptoExtensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string extension)
            {
                return new Tuple<string, string>("cryptoExtension", extension);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for priority extension items
    public class PriorityExtensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string extension)
            {
                return new Tuple<string, string>("priorityExtension", extension);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    // Converter for multiplying values (used for layout scaling)
    public class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return 0;
                
            double doubleValue;
            double multiplier;
            
            // Try to convert the value to double
            if (value is double)
                doubleValue = (double)value;
            else if (!double.TryParse(value.ToString(), out doubleValue))
                return 0;
                
            // Try to convert the parameter to double
            if (parameter is double)
                multiplier = (double)parameter;
            else if (!double.TryParse(parameter.ToString(), out multiplier))
                return 0;
                
            return doubleValue * multiplier;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 