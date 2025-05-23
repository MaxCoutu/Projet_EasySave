using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Projet.Model;
using Projet.ViewModel;
using WpfApp;

namespace Projet.Wpf.View
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(App.BackupService, App.LanguageService, App.PathProvider);
            DataContext = _vm;
            
           
        }
    }
}