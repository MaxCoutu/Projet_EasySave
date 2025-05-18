using System;
using System.Windows.Data;
using Projet.ViewModel;

namespace Projet.Wpf.View
{
    public class ViewModelTypeConverter : IValueConverter
    {
        public static readonly ViewModelTypeConverter Instance = new ViewModelTypeConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.GetType().Name;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}