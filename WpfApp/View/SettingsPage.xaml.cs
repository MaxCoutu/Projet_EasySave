using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Projet.Wpf.View
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private void BlockingProgramInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // This can be removed if not used, or kept for future use
        }

        private void TextBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (tb.Text == tb.Tag?.ToString())
                {
                    tb.Text = string.Empty;
                    tb.Foreground = Brushes.White; // Or any other active text color
                }
            }
        }

        private void TextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = tb.Tag?.ToString();
                    tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7B78AA")); // Placeholder color
                }
            }
        }

        public void ResetBlockingProgramInput()
        {
            BlockingProgramInput.Text = BlockingProgramInput.Tag?.ToString();
            BlockingProgramInput.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7B78AA"));
        }

        public void ResetExtensionInput()
        {
            ExtensionInput.Text = ExtensionInput.Tag?.ToString();
            ExtensionInput.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7B78AA"));
        }

        public void ResetRemoveBlockingProgramInput()
        {
            RemoveBlockingProgramInput.Text = RemoveBlockingProgramInput.Tag?.ToString();
            RemoveBlockingProgramInput.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7B78AA"));
        }

        public void ResetRemoveExtensionInput()
        {
            RemoveExtensionInput.Text = RemoveExtensionInput.Tag?.ToString();
            RemoveExtensionInput.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7B78AA"));
        }

        private void AddBlockingProgram_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetBlockingProgramInput();
            }), DispatcherPriority.ContextIdle, null);
        }

        private void AddExtension_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetExtensionInput();
            }), DispatcherPriority.ContextIdle, null);
        }

        private void RemoveBlockingProgram_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetRemoveBlockingProgramInput();
            }), DispatcherPriority.ContextIdle, null);
        }

        private void RemoveExtension_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetRemoveExtensionInput();
            }), DispatcherPriority.ContextIdle, null);
        }
    }
} 