using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
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
    }
}