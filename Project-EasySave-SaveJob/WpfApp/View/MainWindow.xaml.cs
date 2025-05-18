using System.Windows;
using Projet.ViewModel;
using WpfApp;

namespace Projet.Wpf.View
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(App.BackupService, App.LanguageService, App.PathProvider);
            DataContext = _vm;

           
            _vm.AddJobRequested += ShowAddJobView;

            
            var addJobVm = new AddJobViewModel(App.BackupService);
            addJobVm.JobAdded += () =>
            {
                this.Content = new MainWindow().Content;
                _vm.RefreshJobs(); 
            };
        }

        private void ShowAddJobView()
        {
            var addJobView = new AddJobView();
            addJobView.DataContext = new AddJobViewModel(App.BackupService);
            this.Content = addJobView;
        }
    }
}