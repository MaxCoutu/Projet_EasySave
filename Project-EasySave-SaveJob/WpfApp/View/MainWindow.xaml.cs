using Projet.ViewModel;
using System.Windows;
using System.Windows.Controls;
using WpfApp;

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
            // Remplace le contenu principal par le UserControl AddJobView
            var view = new AddJobView(_vm);
            this.Content = view;
        }

        private void ShowRemoveJobView()
        {
            // Remplace le contenu principal par le UserControl RemoveJobView
            var view = new RemoveJobView(_vm);
            this.Content = view;
        }
    }
}
