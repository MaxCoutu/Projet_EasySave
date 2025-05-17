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

            // Injecter BackupService, LanguageService et PathProvider
            _vm = new MainViewModel(
                App.BackupService,
                App.LanguageService,
                App.PathProvider);

            DataContext = _vm;

            // vos événements existants pour changer de vue
            _vm.AddJobRequested += ShowAddJobView;
            _vm.RemoveJobRequested += ShowRemoveJobView;
        }

        private void ShowAddJobView()
        {
            var view = new AddJobView(_vm);
            this.Content = view;
        }

        private void ShowRemoveJobView()
        {
            var view = new RemoveJobView(_vm);
            this.Content = view;
        }
    }
}
