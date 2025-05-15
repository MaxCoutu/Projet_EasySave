using Projet.ViewModel;
using System.Windows;

namespace Projet.Wpf.View
{
    public partial class RemoveJobView : Window
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
                Close();
            };
        }

    }
}
