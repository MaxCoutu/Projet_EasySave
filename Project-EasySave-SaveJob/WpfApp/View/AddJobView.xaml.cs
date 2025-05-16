using System.Windows.Controls;
using Projet.ViewModel;

namespace Projet.Wpf.View
{
    /// <summary>
    /// Logique d'interaction pour AddJobView.xaml
    /// </summary>
    public partial class AddJobView : UserControl
    {
        // Constructeur sans paramètre requis par WPF
        public AddJobView()
        {
            InitializeComponent();
            // Optionnel : DataContext par défaut si jamais utilisé sans paramètre
            // DataContext = new AddJobViewModel(...); // à adapter si besoin
        }

        public AddJobView(MainViewModel mainVm)
        {
            InitializeComponent();

            var vm = new AddJobViewModel(mainVm.Svc);
            DataContext = vm;

            vm.JobAdded += () =>
            {
                mainVm.RefreshJobs();
                // Navigation vers la vue principale
                mainVm.CurrentViewModel = mainVm; // ou la vue principale selon ta logique
            };
        }
    }
}