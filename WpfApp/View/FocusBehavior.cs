using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Projet.Wpf.View
{
    public static class FocusBehavior
    {
        public static readonly DependencyProperty ClearFocusProperty =
            DependencyProperty.RegisterAttached(
                "ClearFocus",
                typeof(bool),
                typeof(FocusBehavior),
                new PropertyMetadata(false, OnClearFocusChanged));

        public static bool GetClearFocus(DependencyObject obj)
        {
            return (bool)obj.GetValue(ClearFocusProperty);
        }

        public static void SetClearFocus(DependencyObject obj, bool value)
        {
            obj.SetValue(ClearFocusProperty, value);
        }

        private static void OnClearFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button button && (bool)e.NewValue)
            {
                button.Click += Button_Click;
            }
        }

        private static void Button_Click(object sender, RoutedEventArgs e)
        {
            // Clear focus from the button
            Keyboard.ClearFocus();
            
            // Set focus to the parent window
            if (sender is Button button)
            {
                Window parentWindow = Window.GetWindow(button);
                if (parentWindow != null)
                {
                    parentWindow.Focus();
                }
            }
        }
    }
} 