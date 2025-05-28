using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using Projet.Model;
using Projet.ViewModel;
using WpfApp;

namespace Projet.Wpf.View
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(App.BackupService, App.LanguageService, App.PathProvider);
            DataContext = _vm;
            
            // Refresh jobs when window is loaded
            this.Loaded += MainWindow_Loaded;
            
            // Refresh jobs whenever the window is activated (brought to the foreground)
            this.Activated += MainWindow_Activated;
            
            // Add handler for mouse clicks to ensure focus is cleared
            this.MouseDown += MainWindow_MouseDown;
            
            // Listen for layout updates to ensure UI properly refreshes
            this.LayoutUpdated += MainWindow_LayoutUpdated;
            
            // Add handler for all button clicks to ensure proper focus clearing
            this.AddHandler(Button.ClickEvent, new RoutedEventHandler(Button_Click), true);
            
            // Set focus management properties for the window
            System.Windows.Input.KeyboardNavigation.SetTabNavigation(this, System.Windows.Input.KeyboardNavigationMode.None);
            System.Windows.Input.KeyboardNavigation.SetControlTabNavigation(this, System.Windows.Input.KeyboardNavigationMode.None);
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Clear focus immediately after any button click
            Console.WriteLine("Button clicked - clearing focus");
            Keyboard.ClearFocus();
            this.Focus();
            
            // Force a more aggressive focus clearing
            FocusManager.SetFocusedElement(this, null);
            
            // Get the clicked button
            if (sender is Button clickedButton)
            {
                Console.WriteLine($"Button clicked: {clickedButton.ToolTip}");
                
                // Force a refresh immediately
                _vm.ForceRefreshJobs();
                
                // And then after a short delay
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(100);
                timer.Tick += (s, args) => {
                    _vm.ForceRefreshJobs();
                    timer.Stop();
                };
                timer.Start();
            }
            else
            {
                // Force a refresh of the UI after a short delay
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(50);
                timer.Tick += (s, args) => {
                    _vm.ForceRefreshJobs();
                    timer.Stop();
                };
                timer.Start();
            }
        }
        
        // Special handler for job control buttons (pause/resume/stop)
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
            // First refresh after 200ms
            var timer1 = new DispatcherTimer();
            timer1.Interval = TimeSpan.FromMilliseconds(200);
            timer1.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing first post-control refresh");
                _vm.ForceRefreshJobs();
                timer1.Stop();
            };
            timer1.Start();
            
            // Second refresh after 500ms
            var timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromMilliseconds(500);
            timer2.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing second post-control refresh");
                _vm.ForceRefreshJobs();
                timer2.Stop();
            };
            timer2.Start();
            
            // Third refresh after 1000ms to ensure everything is settled
            var timer3 = new DispatcherTimer();
            timer3.Interval = TimeSpan.FromMilliseconds(1000);
            timer3.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing final post-control refresh");
                _vm.ForceRefreshJobs();
                timer3.Stop();
            };
            timer3.Start();
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure jobs are refreshed when the window is loaded
            _vm.RefreshJobs();
            
            // Force loading of all jobs into RecentJobs
            var allJobs = App.BackupService.GetJobs();
            if (allJobs.Count > 0)
            {
                _vm.RecentJobs.Clear();
                foreach (var job in allJobs)
                {
                    _vm.RecentJobs.Add(job);
                }
                
                // Update job status
                typeof(MainViewModel).GetMethod("UpdateJobStatusInternal", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance)?.Invoke(_vm, null);
            }
        }
        
        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // Use the new ForceRefreshJobs method
            Console.WriteLine("MainWindow activated - using ForceRefreshJobs method");
            _vm.ForceRefreshJobs();
        }
        
        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clear focus when clicking anywhere in the window
            Keyboard.ClearFocus();
            this.Focus();
            
            // Force a more aggressive focus clearing
            FocusManager.SetFocusedElement(this, null);
        }
        
        // Track layout updates
        private DateTime _lastLayoutUpdate = DateTime.MinValue;
        
        private void MainWindow_LayoutUpdated(object sender, EventArgs e)
        {
            // Only refresh at most once per second to avoid performance issues
            if ((DateTime.Now - _lastLayoutUpdate).TotalSeconds >= 1)
            {
                _lastLayoutUpdate = DateTime.Now;
                // Only refresh if we're in the main view (not showing a dialog)
                if (_vm.CurrentViewModel == _vm)
                {
                    Console.WriteLine("Layout updated - refreshing jobs");
                    _vm.ForceRefreshJobs();
                }
            }
        }
    }
}