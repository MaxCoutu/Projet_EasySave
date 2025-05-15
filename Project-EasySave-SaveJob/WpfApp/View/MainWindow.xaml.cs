using Projet.ViewModel;
using System.Windows;
using WpfApp;  // <- important pour accéder à App

namespace Projet.Wpf.View
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel(App.BackupService);
            DataContext = _vm;

            _vm.AddJobRequested += ShowAddJobView;
            _vm.RemoveJobRequested += ShowRemoveJobView;
        }

        private void ShowAddJobView()
        {
            var view = new AddJobView(_vm);
            view.ShowDialog();
        }

        private void ShowRemoveJobView()
        {
            var view = new RemoveJobView(_vm);
            view.ShowDialog();
        }
    }
}
