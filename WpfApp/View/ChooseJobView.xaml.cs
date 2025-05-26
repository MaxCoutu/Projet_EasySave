using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Projet.Model;
using Projet.ViewModel;

namespace Projet.Wpf.View
{
    public partial class ChooseJobView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ChooseJobViewModel _viewModel;

        public ChooseJobView()
        {
            InitializeComponent();
            
            this.Loaded += (s, e) =>
            {
                _viewModel = DataContext as ChooseJobViewModel;
                // Le ProgressPanel est dans un DataTemplate et n'est pas accessible directement
            };
            
            // Plus besoin de la méthode de mise à jour, car le convertisseur fait tout le travail
        }
    }
}