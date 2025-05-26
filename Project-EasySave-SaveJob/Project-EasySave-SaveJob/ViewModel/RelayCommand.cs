using System;
using System.Windows.Input;

namespace Projet.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _exec;
        private readonly Predicate<object> _canExec;

        public RelayCommand(Action<object> exec) 
        {
            _exec = exec;
            _canExec = null;
        }
        
        public RelayCommand(Action<object> exec, Predicate<object> canExec)
        {
            _exec = exec;
            _canExec = canExec;
        }

        public bool CanExecute(object parameter) => _canExec == null || _canExec(parameter);
        public void Execute(object p) => _exec(p);
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
