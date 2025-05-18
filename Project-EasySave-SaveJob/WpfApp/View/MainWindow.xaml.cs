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
        }
    }
}