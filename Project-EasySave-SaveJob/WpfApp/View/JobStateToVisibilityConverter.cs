using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Projet.Model;

namespace Projet.Wpf.View
{
    public class JobStateToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string state && parameter is string buttonType)
            {
                // Print debug info to help diagnose conversion issues
                Console.WriteLine($"JobStateToVisibilityConverter: state={state}, buttonType={buttonType}");
                
                switch (buttonType)
                {
                    case "Play":
                        // Play button visible when job is ready, ended, or cancelled
                        var playVisible = (state == "READY" || state == "END" || state == "CANCELLED" || state == "ERROR");
                        Console.WriteLine($"Play button visibility: {playVisible}");
                        return playVisible ? Visibility.Visible : Visibility.Hidden;
                    
                    case "PauseResume":
                        // Pause/Resume button visible when job is active, paused or pending
                        var pauseVisible = (state == "ACTIVE" || state == "PAUSED" || state == "PENDING");
                        Console.WriteLine($"Pause button visibility: {pauseVisible}");
                        return pauseVisible ? Visibility.Visible : Visibility.Hidden;
                    
                    case "Stop":
                        // Stop button visible when job is active, paused, or pending
                        var stopVisible = (state == "ACTIVE" || state == "PAUSED" || state == "PENDING");
                        Console.WriteLine($"Stop button visibility: {stopVisible}");
                        return stopVisible ? Visibility.Visible : Visibility.Hidden;
                }
            }
            else
            {
                Console.WriteLine($"JobStateToVisibilityConverter: Invalid parameters - value={value}, parameter={parameter}");
            }
            
            // Default to hidden (not collapsed, to maintain layout)
            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        
        // Static instance for ease of use
        public static readonly JobStateToVisibilityConverter Instance = new JobStateToVisibilityConverter();
    }
} 