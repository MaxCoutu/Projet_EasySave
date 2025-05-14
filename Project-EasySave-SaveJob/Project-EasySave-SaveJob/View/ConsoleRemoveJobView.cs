using System;
using Projet.Service;
using Projet.ViewModel;

namespace Projet.View
{
    public class ConsoleRemoveJobView : IRemoveJobView
    {
        private readonly RemoveJobViewModel _vm;
        private readonly IBackupService _svc;

        public ConsoleRemoveJobView(RemoveJobViewModel vm, IBackupService svc)
        {
            _vm = vm;
            _svc = svc;
        }

        public void Show()
        {
            Console.Clear();
            var jobs = _svc.GetJobs();
            if (jobs.Count == 0)
            {
                Console.WriteLine("No job defined.");
                return;
            }

            Console.WriteLine("Select job to remove:");
            for (int i = 0; i < jobs.Count; i++)
                Console.WriteLine($"{i + 1}. {jobs[i].Name}");

            if (int.TryParse(Console.ReadLine(), out int idx) &&
                idx > 0 && idx <= jobs.Count)
            {
                _vm.SelectedJob = jobs[idx - 1];
                _vm.Remove();
                Console.WriteLine("Removed.");
            }
        }

        public void Close() { }
    }
}
