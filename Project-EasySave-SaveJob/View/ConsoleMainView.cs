using System;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;
using Projet.ViewModel;

namespace Projet.View
{
    public class ConsoleMainView : IMainView
    {
        private readonly MainViewModel    _vm;
        private readonly ILanguageService _lang;
        private readonly IAddJobView      _add;
        private readonly IRemoveJobView   _remove;
        private readonly IBackupService   _svc;

        public ConsoleMainView(
            MainViewModel    vm,
            ILanguageService lang,
            IAddJobView      add,
            IRemoveJobView   remove,
            IBackupService   svc)
        {
            _vm     = vm;
            _lang   = lang;
            _add    = add;
            _remove = remove;
            _svc    = svc;

            _svc.StatusUpdated += OnStatus;
        }

        public void Show()
        {
            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                Console.WriteLine(_lang.Translate("menu_title"));
                Console.WriteLine(_lang.Translate("menu_list"));
                Console.WriteLine(_lang.Translate("menu_runselected"));
                Console.WriteLine(_lang.Translate("menu_runall"));
                Console.WriteLine(_lang.Translate("menu_add"));
                Console.WriteLine(_lang.Translate("menu_remove"));
                Console.WriteLine(_lang.Translate("menu_exit"));
                Console.Write(_lang.Translate("menu_choice"));

                switch (Console.ReadLine())
                {
                    case "1":
                        Console.Clear(); ListJobs();     Pause(); break;

                    case "2":
                        Console.Clear(); SelectJob();
                        RunSelectedJob().Wait();
                        Pause(); break;

                    case "3":
                        Console.Clear();
                        _svc.ExecuteAllBackupsAsync().Wait();
                        Pause(); break;

                    case "4":
                        Console.Clear(); _add.Show(); Refresh(); Pause(); break;

                    case "5":
                        Console.Clear(); _remove.Show(); Refresh(); Pause(); break;

                    case "0": exit = true; break;

                    default:
                        Console.WriteLine(_lang.Translate("invalid_choice"));
                        Pause(); break;
                }
            }
        }
        public void Close() { }

        /* ---------- jobs list ---------- */
        private void ListJobs()
        {
            Console.WriteLine(_lang.Translate("jobs_label"));
            Console.WriteLine(new string('─', 60));

            for (int i = 0; i < _vm.Jobs.Count; i++)
            {
                BackupJob j = _vm.Jobs[i];
                Console.WriteLine($"{i + 1}. {j.Name}");
                Console.WriteLine($"   Source : {j.SourceDir}");
                Console.WriteLine($"   Target : {j.TargetDir}");
                Console.WriteLine($"   Type   : {j.Strategy.Type}");
                Console.WriteLine(new string('─', 60));
            }
        }

        private void SelectJob()
        {
            ListJobs();
            Console.Write(_lang.Translate("menu_choice"));
            if (int.TryParse(Console.ReadLine(), out int idx) &&
                idx > 0 && idx <= _vm.Jobs.Count)
                _vm.SelectedJob = _vm.Jobs[idx - 1];
        }

        /* ---------- run helpers ---------- */
        private Task RunSelectedJob()
        {
            return _vm.SelectedJob == null
                ? Task.CompletedTask
                : _svc.ExecuteBackupAsync(_vm.SelectedJob.Name);
        }

        /* ---------- utils ---------- */
        private void Refresh()
        {
            _vm.Jobs.Clear();
            foreach (BackupJob j in _svc.GetJobs())
                _vm.Jobs.Add(j);
        }

        private void Pause()
        {
            Console.WriteLine();
            Console.WriteLine(_lang.Translate("press_key"));
            Console.ReadKey();
        }

        private static void OnStatus(StatusEntry s)
        {
            Console.CursorLeft = 0;
            Console.Write($"{s.Name}: {s.Progression:P0} " +
                          $"({s.TotalFilesToCopy - s.NbFilesLeftToDo}/{s.TotalFilesToCopy})   ");
        }
    }
}
