using System;
using System.Globalization;
using System.Windows.Data;

namespace Projet.Wpf.View
{
    public class FolderNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path))
            {
                // Supprime les guillemets au d�but et � la fin du chemin
                return path.Trim('"');
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}