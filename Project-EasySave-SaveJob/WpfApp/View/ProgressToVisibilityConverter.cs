using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Projet.Model;

namespace Projet.Wpf.View
{
    
    public class ProgressToVisibilityConverter : IValueConverter
    {
        #region Conversion de base
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            
            if (parameter is string strParam)
            {
                switch (strParam)
                {
                    case "calculate-copied":
                        return CalculateFilesCopied(value);
                    case "visibility-string":
                        return ConvertStringToVisibility(value);
                    case "visibility-job":
                        return ConvertJobToVisibility(value);
                    case "visibility-value":
                        return ConvertValueToVisibility(value);
                }
            }

           
            if (value is string state)
            {
                return ConvertStringToVisibility(state);
            }
            
            // Si c'est un job
            if (value is BackupJob job)
            {
                return ConvertJobToVisibility(job);
            }

            // Autres types de valeurs
            if (value != null)
            {
                if (double.TryParse(value.ToString(), out double numValue) && numValue > 0)
                {
                    return Visibility.Visible;
                }
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Méthodes de conversion spécifiques
        
        /// <summary>
        /// Convertit une chaîne (état) en visibilité
        /// </summary>
        private object ConvertStringToVisibility(object value)
        {
            if (value is string state)
            {
                // Visible pour les états ACTIVE, PENDING et PAUSED
                if (state == "ACTIVE" || state == "PENDING" || state == "PAUSED")
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }
        
        /// <summary>
        /// Convertit un job en visibilité basée sur son état
        /// </summary>
        private object ConvertJobToVisibility(object value)
        {
            if (value is BackupJob job)
            {
                // Visible pour les états ACTIVE, PENDING, PAUSED ou si la progression est > 0
                if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED" || 
                    (job.Progression > 0 && job.State != "END" && job.State != "CANCELLED"))
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }
        
        /// <summary>
        /// Convertit une valeur numérique en visibilité (visible si >0)
        /// </summary>
        private object ConvertValueToVisibility(object value)
        {
            if (value != null)
            {
                if (int.TryParse(value.ToString(), out int numValue) && numValue > 0)
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }
        
        /// <summary>
        /// Calcule le nombre de fichiers copiés (total - restants)
        /// </summary>
        private object CalculateFilesCopied(object value)
        {
            if (value is BackupJob job && job.TotalFilesToCopy > 0)
            {
                int copied = job.TotalFilesToCopy - job.NbFilesLeftToDo;
                return copied.ToString();
            }
            return "0";
        }
        #endregion
        
        #region Instances statiques pour différentes opérations
        public static readonly ProgressToVisibilityConverter VisibilityConverter = new ProgressToVisibilityConverter();
        public static readonly ProgressToVisibilityConverter FilesCopiedCalculator = new ProgressToVisibilityConverter();
        #endregion
    }
} 