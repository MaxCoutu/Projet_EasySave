using Projet.Infrastructure;
using Projet.ViewModel;
using Projet.Model;
using Projet.Service;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.ComponentModel;
using System.Windows.Threading;
using WpfApp;

namespace Client
{

    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public ObservableCollection<StatusEntry> Jobs { get; } = new ObservableCollection<StatusEntry>();

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(App.BackupService, App.LanguageService, App.PathProvider);


            DataContext = _vm;
            Loaded += async (s, e) => await _vm.LoadJobsAsync(UpdateJobs);
        }

        private void UpdateJobs(List<StatusEntry> jobs)
        {
            Dispatcher.Invoke(() =>
            {
                Jobs.Clear();
                foreach (var job in jobs)
                    Jobs.Add(job);
            });
        }

        private void ControlButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[CRITICAL] Job control button clicked!");

            // Identify which button was clicked
            if (sender is Button clickedButton)
            {
                Console.WriteLine($"[CRITICAL] Button type: {clickedButton.ToolTip}, Tag: {clickedButton.Tag}");

                // If this is a stop button, we need special handling to ensure play button becomes visible
                if (clickedButton.ToolTip.ToString().Contains("Stop"))
                {
                    Console.WriteLine("[CRITICAL] STOP BUTTON detected - ensuring play button will appear");

                    // The stop command will be executed after this handler
                    // Schedule a series of refreshes to make sure UI updates correctly
                    ScheduleMultipleRefreshes();
                }
                else if (clickedButton.ToolTip.ToString().Contains("Pause"))
                {
                    Console.WriteLine("[CRITICAL] PAUSE/RESUME BUTTON detected");

                    // For pause/resume, we also want multiple refreshes
                    ScheduleMultipleRefreshes();
                }
            }

            // Clear focus immediately
            Keyboard.ClearFocus();
            this.Focus();
            FocusManager.SetFocusedElement(this, null);

            // Force immediate UI update
            _vm.ForceRefreshJobs();
        }

        // Helper method to schedule multiple refreshes with increasing delays
        private void ScheduleMultipleRefreshes()
        {
            // First refresh after 50ms
            var timer1 = new DispatcherTimer();
            timer1.Interval = TimeSpan.FromMilliseconds(50);
            timer1.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing first post-control refresh");
                _vm.ForceRefreshJobs();
                timer1.Stop();
            };
            timer1.Start();

            // Second refresh after 150ms
            var timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromMilliseconds(150);
            timer2.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing second post-control refresh");
                _vm.ForceRefreshJobs();
                timer2.Stop();
            };
            timer2.Start();

            // Third refresh after 300ms to ensure everything is settled
            var timer3 = new DispatcherTimer();
            timer3.Interval = TimeSpan.FromMilliseconds(300);
            timer3.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing final post-control refresh");
                _vm.ForceRefreshJobs();
                timer3.Stop();
            };
            timer3.Start();
        }
    }
}