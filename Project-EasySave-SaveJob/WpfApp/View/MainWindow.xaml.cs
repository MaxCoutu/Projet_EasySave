using Projet.ViewModel;
using System.Windows;

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
        }

        private void RemoveJob_Click(object sender, RoutedEventArgs e)
        {
            // Récupère le job associé à la ligne du bouton cliqué
            var job = (sender as FrameworkElement)?.DataContext as BackupJob;
            if (job == null) return;

            var result = MessageBox.Show(
                $"Voulez-vous vraiment supprimer la tâche '{job.Name}' ?",
                "Confirmation de suppression",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _vm.RemoveJobCommand.Execute(job);
            }
        }
    }
}
