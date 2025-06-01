using System;
using System.Windows.Input;
using Projet.Infrastructure;

namespace Projet.ViewModel
{
    public class DecryptorViewModel : ViewModelBase
    {
        // Changer en propriété au lieu d'un champ readonly
        public MainViewModel MainViewModel { get; }
        
        public DecryptorViewModel(MainViewModel mainViewModel)
        {
            MainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            
            // Initialiser les commandes
            ReturnToMainViewCommand = new RelayCommand(_ => ReturnToMainView());
        }
        
        /// <summary>
        /// Commande pour retourner à la vue principale
        /// </summary>
        public ICommand ReturnToMainViewCommand { get; }
        
        /// <summary>
        /// Retourne à la vue principale
        /// </summary>
        private void ReturnToMainView()
        {
            MainViewModel.CurrentViewModel = MainViewModel;
        }
    }
} 