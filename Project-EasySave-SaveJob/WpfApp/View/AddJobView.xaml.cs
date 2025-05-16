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
        }

        public AddJobView(MainViewModel mainVm)
        {
            InitializeComponent();

            var vm = new AddJobViewModel(mainVm.Svc);
            DataContext = vm;

            vm.JobAdded += () =>
            {
                mainVm.RefreshJobs();
                // Pour un UserControl, il n'y a pas de Close()
                // Si tu veux masquer le contrôle, tu peux faire :
                // this.Visibility = Visibility.Collapsed;
            };
        }
    }
}
