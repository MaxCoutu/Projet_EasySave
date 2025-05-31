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
                    case "progress-fraction":
                        return ConvertProgressToFraction(value);
                    case "refresh-counter":
                        return ConvertRefreshCounter(value);
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
                // Rendre la barre visible pour tous les états actifs, y compris PAUSED
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
                // Toujours rendre la barre visible pour les jobs actifs ou en pause, ou avec progression
                if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                {
                    return Visibility.Visible;
                }
                
                // Si le job est terminé (END, CANCELLED, ERROR), masquer la barre même si progression = 100%
                if (job.State == "END" || job.State == "CANCELLED" || job.State == "ERROR")
                {
                    return Visibility.Collapsed;
                }
                
                // Pour les autres états, rendre visible si la progression > 0
                if (job.Progression > 0)
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
        /// Convertit une valeur de progression (0-100) en fraction (0-1) pour la ProgressBar
        /// </summary>
        private object ConvertProgressToFraction(object value)
        {
            if (value != null)
            {
                if (double.TryParse(value.ToString(), out double numValue))
                {
                    // S'assurer que la valeur est entre 0 et 100
                    numValue = Math.Min(100, Math.Max(0, numValue));
                    
                    // S'assurer que quand la progression est à 100%, la barre est bien remplie
                    if (numValue >= 99.9)
                    {
                        return 1.0; // Forcer à 1.0 pour garantir que la barre soit complètement remplie
                    }
                    
                    // Divise la valeur par 100 pour convertir de pourcentage à fraction
                    return numValue / 100.0;
                }
            }
            return 0.0;
        }
        
        /// <summary>
        /// Traite la propriété RefreshCounter en retournant une string unique à chaque changement
        /// pour forcer le rafraîchissement visuel
        /// </summary>
        private object ConvertRefreshCounter(object value)
        {
            if (value != null && int.TryParse(value.ToString(), out int counter))
            {
                // Utiliser une valeur unique pour forcer le rafraîchissement
                return $"refresh_{counter}_{DateTime.Now.Ticks}";
            }
            return "refresh_0";
        }
        
        /// <summary>
        /// Calcule le nombre de fichiers copiés (total - restants)
        /// </summary>
        private object CalculateFilesCopied(object value)
        {
            if (value is BackupJob job && job.TotalFilesToCopy > 0)
            {
                // S'assurer que le nombre de fichiers copiés est cohérent et jamais négatif
                int copied = Math.Max(0, Math.Min(job.TotalFilesToCopy, job.TotalFilesToCopy - job.NbFilesLeftToDo));
                
                // Cas spécial: si la progression est à 100%, montrer tous les fichiers comme copiés
                if (job.Progression >= 99.9)
                {
                    return job.TotalFilesToCopy.ToString();
                }
                
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