using System;
using System.Globalization;
using System.Windows.Data;
using Projet.ViewModel;

namespace Projet.Wpf.View
{
    /// <summary>
    /// Convertit un ViewModel en son nom de type pour faciliter l'utilisation des DataTriggers
    /// </summary>
    public class ViewModelTypeConverter : IValueConverter
    {
        // Singleton pour optimiser les ressources
        private static readonly ViewModelTypeConverter _instance = new ViewModelTypeConverter();
        public static ViewModelTypeConverter Instance => _instance;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;
            
            // Retourne simplement le nom de la classe du ViewModel
            return value.GetType().Name;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}