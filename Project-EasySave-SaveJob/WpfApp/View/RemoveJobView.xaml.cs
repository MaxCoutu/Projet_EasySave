using System.Windows.Controls;
using Projet.ViewModel;

namespace Projet.Wpf.View
{
    /// <summary>
    /// Logique d'interaction pour RemoveJobView.xaml
    /// </summary>
    public partial class RemoveJobView : UserControl
    {
        public RemoveJobView(MainViewModel mainVm)
        {
            InitializeComponent();

            var vm = new RemoveJobViewModel(mainVm.Svc);

            vm.Jobs.Clear();
            foreach (var job in mainVm.Jobs)
            {
                vm.Jobs.Add(job);
            }

            DataContext = vm;

            vm.JobRemoved += () =>
            {
                mainVm.RefreshJobs();
                // Pour un UserControl, il n'y a pas de Close()
                // this.Visibility = Visibility.Collapsed; // si besoin
            };
        }
    }
}
