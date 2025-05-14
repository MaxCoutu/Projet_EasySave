using System;
using System.Windows.Input;

namespace Projet.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _exec;
        public RelayCommand(Action<object> exec) => _exec = exec;

        public bool CanExecute(object _) => true;
        public void Execute(object p) => _exec(p);
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
