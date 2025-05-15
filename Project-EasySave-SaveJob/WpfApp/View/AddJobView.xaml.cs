using Projet.ViewModel;
using System.Windows;

namespace Projet.Wpf.View
{
    public partial class AddJobView : Window
    {
        public AddJobView(MainViewModel mainVm)
        {
            InitializeComponent();

            var vm = new AddJobViewModel(mainVm.Svc);
            DataContext = vm;

            vm.JobAdded += () =>
            {
                mainVm.RefreshJobs();
                Close();
            };
        }
    }
}
