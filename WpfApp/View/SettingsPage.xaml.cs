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
        
        private void ResetTextBox(TextBox textBox)
        {
            if (textBox != null)
            {
                textBox.Text = textBox.Tag?.ToString();
                textBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7B78AA"));
            }
        }

        private void AddBlockingProgram_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetTextBox(BlockingProgramInput);
            }), DispatcherPriority.ContextIdle, null);
        }

        private void AddExtension_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetTextBox(ExtensionInput);
            }), DispatcherPriority.ContextIdle, null);
        }

        private void AddPriorityExtension_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetTextBox(PriorityExtensionInput);
            }), DispatcherPriority.ContextIdle, null);
        }
    }
} 