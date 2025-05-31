using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using Projet.Model;
using Projet.ViewModel;
using System.Windows.Media.Animation; // Ajout pour les animations

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
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _forceRefreshTimer; // Nouveau timer pour forcer la relecture complète
        private DispatcherTimer _mouseHoverSimulationTimer; // Simule le survol de souris sur les boutons
        private DispatcherTimer _progressBarRefreshTimer; // Nouveau timer spécifique pour les barres de progression
        private int _refreshCounter = 0;
        private bool _forceVisibility = true; // Forcer la visibilité des barres de progression
        private Random _rnd = new Random(); // Pour les micro-variations aléatoires
        private DateTime _lastRenderTime = DateTime.Now; // Pour limiter les rendus

        public ChooseJobView()
        {
            InitializeComponent();
            
            // S'abonner à l'événement de rendu global pour forcer les rafraîchissements
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            
            // Add handler for all button clicks to ensure proper focus clearing
            this.AddHandler(Button.ClickEvent, new RoutedEventHandler(ControlButton_Click), true);
            
            this.Loaded += (s, e) =>
            {
                _viewModel = DataContext as ChooseJobViewModel;
                
                // Créer un timer TRÈS agressif pour le rafraîchissement
                _refreshTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS pour une mise à jour très fluide
                };
                
                _refreshTimer.Tick += (sender, args) =>
                {
                    if (_viewModel == null) return;
                    
                    // Incrémenter le compteur
                    _refreshCounter++;
                    
                    // CRUCIAL: Forcer une relecture complète du status.json AVANT de rafraîchir l'UI
                    // C'est exactement ce qui se passe lors d'une transition pause/resume
                    if (_refreshCounter % 3 == 0) // Toutes les ~50ms (3 * 16ms)
                    {
                        _viewModel.ForceRefreshFromJsonFile();
                    }
                    
                    // Forcer le rafraîchissement de TOUS les jobs
                    foreach (var job in _viewModel.Jobs)
                    {
                        // TOUJOURS déclencher un rafraîchissement des jobs actifs, en pause, ou avec progression
                        if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED" || job.Progression > 0)
                        {
                            // Forcer l'incrémentation du compteur de rafraîchissement 
                            // pour déclencher l'animation
                            job.ForceProgressRefresh();
                            
                            // Forcer l'actualisation des éléments visuels en utilisant Dispatcher
                            Application.Current.Dispatcher.Invoke(() => 
                            {
                                try
                                {
                                    // Trouver le conteneur correspondant au job
                                    var item = JobsItemsControl.ItemContainerGenerator.ContainerFromItem(job) as FrameworkElement;
                                    if (item != null)
                                    {
                                        // Trouver la ProgressBar et le panneau parent
                                        var progressPanel = FindVisualChild<Grid>(item, "ProgressPanel");
                                        if (progressPanel != null)
                                        {
                                            // COMPORTEMENT CRUCIAL:
                                            // Lors d'une transition pause/resume, le panneau devient visible
                                            progressPanel.Visibility = Visibility.Visible;
                                            
                                            // Trouver et rafraîchir la barre de progression
                                            var progressBar = FindVisualChild<ProgressBar>(progressPanel, "JobProgressBar");
                                            if (progressBar != null)
                                            {
                                                // Forcer le rafraîchissement visuel avec une micro-animation
                                                ForceProgressBarUpdate(progressBar, job.Progression / 100.0);
                                                
                                                // Force tous les éléments parents à se rafraîchir également
                                                InvalidateParentVisuals(progressBar);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error refreshing UI: {ex.Message}");
                                }
                            }, DispatcherPriority.Render);
                        }
                    }
                };
                
                // Nouveau timer pour forcer une relecture complète du status.json
                // Exactement comme lors d'une transition pause/resume
                _forceRefreshTimer = new DispatcherTimer(DispatcherPriority.Normal)
                {
                    Interval = TimeSpan.FromMilliseconds(120) // Toutes les 120ms
                };
                
                _forceRefreshTimer.Tick += (sender, args) =>
                {
                    // Forcer un rafraîchissement complet au niveau du ViewModel
                    if (_viewModel != null)
                    {
                        _viewModel.ForceProgressUpdate();
                    }
                };
                
                // NOUVEAU: Timer pour simuler le mouvement de la souris sur les boutons
                // Cette simulation reproduit exactement ce qui se passe quand vous survolez les boutons
                _mouseHoverSimulationTimer = new DispatcherTimer(DispatcherPriority.Input)
                {
                    Interval = TimeSpan.FromMilliseconds(100) // Toutes les 100ms
                };
                
                _mouseHoverSimulationTimer.Tick += (sender, args) =>
                {
                    try
                    {
                        // Simuler des survols de souris sur tous les jobs actifs
                        foreach (var job in _viewModel.Jobs)
                        {
                            if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                            {
                                // Trouver le conteneur du job
                                var item = JobsItemsControl.ItemContainerGenerator.ContainerFromItem(job) as FrameworkElement;
                                if (item != null)
                                {
                                    SimulateButtonInteraction(item);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in mouse hover simulation: {ex.Message}");
                    }
                };
                
                // Nouveau timer spécifique pour forcer le rafraîchissement des barres de progression
                _progressBarRefreshTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(100) // 10 Hz
                };
                
                _progressBarRefreshTimer.Tick += (sender, args) =>
                {
                    try
                    {
                        // Forcer l'invalidation et le rafraîchissement de toutes les barres de progression
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var job in _viewModel.Jobs)
                            {
                                if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                                {
                                    var item = JobsItemsControl.ItemContainerGenerator.ContainerFromItem(job) as FrameworkElement;
                                    if (item != null)
                                    {
                                        var progressBar = FindVisualChild<ProgressBar>(item, "JobProgressBar");
                                        if (progressBar != null)
                                        {
                                            // Forcer l'invalidation visuelle
                                            progressBar.InvalidateVisual();
                                            progressBar.UpdateLayout();
                                            
                                            // Déclencher une micro-animation pour forcer le rafraîchissement
                                            ForceProgressBarUpdate(progressBar, job.Progression / 100.0);
                                            
                                            // Force tous les éléments parents à se rafraîchir également
                                            InvalidateParentVisuals(progressBar);
                                        }
                                    }
                                }
                            }
                            
                            // Forcer l'invalidation globale pour rafraîchir toute l'interface
                            InvalidateVisual();
                            UpdateLayout();
                        }, DispatcherPriority.Render);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in progress bar refresh: {ex.Message}");
                    }
                };
                
                _refreshTimer.Start();
                _forceRefreshTimer.Start();
                _mouseHoverSimulationTimer.Start();
                _progressBarRefreshTimer.Start();
            };
            
            // S'assurer que le timer est arrêté quand le contrôle est déchargé
            this.Unloaded += (s, e) =>
            {
                _refreshTimer?.Stop();
                _forceRefreshTimer?.Stop();
                _mouseHoverSimulationTimer?.Stop();
                _progressBarRefreshTimer?.Stop();
                
                // Se désabonner de l'événement de rendu
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
            };
        }
        
        // Special handler for job control buttons (pause/resume/stop)
        private void ControlButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            // Identify which button was clicked
            if (sender is Button clickedButton)
            {
                // Get the ToolTip text to identify the button type
                string toolTipText = clickedButton.ToolTip?.ToString() ?? "";
                
                Console.WriteLine($"[CHOOSE JOB VIEW] Button clicked: {toolTipText}");
                
                // If this is a stop button, we need special handling to ensure play button becomes visible
                if (toolTipText.Contains("Stop"))
                {
                    Console.WriteLine("[CHOOSE JOB VIEW] STOP BUTTON detected - ensuring play button will appear");
                    ScheduleMultipleRefreshes();
                }
                // For pause/resume, we also want multiple refreshes
                else if (toolTipText.Contains("Pause"))
                {
                    Console.WriteLine("[CHOOSE JOB VIEW] PAUSE/RESUME BUTTON detected");
                    ScheduleMultipleRefreshes();
                }
                // For the run button
                else if (toolTipText.Contains("play") || toolTipText.Contains("Play"))
                {
                    Console.WriteLine("[CHOOSE JOB VIEW] PLAY BUTTON detected");
                    ScheduleMultipleRefreshes();
                }
            }
            
            // Clear focus immediately
            Keyboard.ClearFocus();
            this.Focus();
            FocusManager.SetFocusedElement(this, null);
            
            // Force immediate UI update
            _viewModel.ForceProgressUpdate();
        }
        
        // Helper method to schedule multiple refreshes with increasing delays
        private void ScheduleMultipleRefreshes()
        {
            if (_viewModel == null) return;
            
            // First refresh after 50ms
            var timer1 = new DispatcherTimer();
            timer1.Interval = TimeSpan.FromMilliseconds(50);
            timer1.Tick += (s, args) => {
                Console.WriteLine("[CHOOSE JOB VIEW] Performing first post-control refresh");
                _viewModel.ForceProgressUpdate();
                timer1.Stop();
            };
            timer1.Start();
            
            // Second refresh after 150ms
            var timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromMilliseconds(150);
            timer2.Tick += (s, args) => {
                Console.WriteLine("[CHOOSE JOB VIEW] Performing second post-control refresh");
                _viewModel.ForceProgressUpdate();
                timer2.Stop();
            };
            timer2.Start();
            
            // Third refresh after 300ms to ensure everything is settled
            var timer3 = new DispatcherTimer();
            timer3.Interval = TimeSpan.FromMilliseconds(300);
            timer3.Tick += (s, args) => {
                Console.WriteLine("[CHOOSE JOB VIEW] Performing final post-control refresh");
                _viewModel.ForceProgressUpdate();
                timer3.Stop();
            };
            timer3.Start();
        }
        
        // Méthode qui s'exécute à chaque frame rendue par WPF - utilise le rendu GPU pour forcer les rafraîchissements
        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            // Limiter la fréquence pour éviter trop de rendus inutiles (1 toutes les 50ms = 20 fps)
            if ((DateTime.Now - _lastRenderTime).TotalMilliseconds < 50) return;
            _lastRenderTime = DateTime.Now;
            
            try
            {
                if (_viewModel != null)
                {
                    // Mettre à jour et forcer le rafraîchissement de tous les jobs actifs
                    foreach (var job in _viewModel.Jobs)
                    {
                        if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                        {
                            // Forcer le compteur de rafraîchissement à s'incrémenter 
                            job.RefreshCounter++;
                            
                            // Chercher la ProgressBar pour ce job
                            var item = JobsItemsControl?.ItemContainerGenerator.ContainerFromItem(job) as FrameworkElement;
                            if (item != null)
                            {
                                // Trouver et rafraîchir la barre de progression
                                var progressBar = FindVisualChild<ProgressBar>(item, "JobProgressBar");
                                if (progressBar != null)
                                {
                                    // Forcer le rafraîchissement visuel avec une micro-animation
                                    Application.Current.Dispatcher.Invoke(() => 
                                    {
                                        ForceProgressBarUpdate(progressBar, job.Progression / 100.0);
                                        
                                        // Force tous les éléments parents à se rafraîchir également
                                        InvalidateParentVisuals(progressBar);
                                    }, DispatcherPriority.Render);
                                }
                            }
                        }
                        // Special case for completed jobs at 100%
                        else if (job.State == "END" && job.Progression >= 99.9)
                        {
                            // Ensure the progress bar is fully filled
                            var item = JobsItemsControl?.ItemContainerGenerator.ContainerFromItem(job) as FrameworkElement;
                            if (item != null)
                            {
                                var progressBar = FindVisualChild<ProgressBar>(item, "JobProgressBar");
                                if (progressBar != null)
                                {
                                    Application.Current.Dispatcher.Invoke(() => 
                                    {
                                        // Force value to 1.0 (100%)
                                        ForceProgressBarUpdate(progressBar, 1.0);
                                    }, DispatcherPriority.Render);
                                }
                            }
                        }
                    }
                    
                    // Toutes les 10 frames environ, force une relecture complète des données
                    if (_rnd.Next(0, 10) == 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                            _viewModel.ForceProgressUpdate();
                        }), DispatcherPriority.Background);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CompositionTarget_Rendering: {ex.Message}");
            }
        }

        // Force tous les parents à se rafraîchir
        private void InvalidateParentVisuals(DependencyObject element)
        {
            var current = element;
            while (current != null && current != this)
            {
                if (current is UIElement uiElement)
                {
                    uiElement.InvalidateVisual();
                }
                
                // Remonter dans l'arborescence
                current = VisualTreeHelper.GetParent(current);
            }
        }
        
        // Nouvelle méthode pour forcer le rafraîchissement d'une barre de progression avec une animation
        private void ForceProgressBarUpdate(ProgressBar progressBar, double value)
        {
            try
            {
                // S'assurer que la valeur est bien entre 0 et 1
                value = Math.Min(1.0, Math.Max(0.0, value));
                
                // Si la progression est à 100%, s'assurer que la barre est complètement remplie
                if (value >= 0.999)
                {
                    value = 1.0;
                }
                
                // Créer une micro-animation qui force le rafraîchissement visuel
                var animation = new DoubleAnimation(
                    value - 0.0001,  // Valeur de départ légèrement différente
                    value,           // Valeur finale
                    new Duration(TimeSpan.FromMilliseconds(50)) // Animation très rapide
                );
                
                // Démarrer l'animation sur la propriété Value
                progressBar.BeginAnimation(ProgressBar.ValueProperty, animation);
                
                // Forcer l'invalidation visuelle
                progressBar.InvalidateVisual();
                progressBar.UpdateLayout();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ForceProgressBarUpdate: {ex.Message}");
            }
        }
        
        private void SimulateButtonInteraction(FrameworkElement container)
        {
            try
            {
                // Trouver les conteneurs des boutons
                var pauseContainer = FindVisualChild<Border>(container, "PauseButtonContainer");
                var stopContainer = FindVisualChild<Border>(container, "StopButtonContainer");
                
                // Activer l'un des boutons au hasard (comme un survol)
                if (_rnd.Next(0, 5) == 0) // 20% de chance
                {
                    if (pauseContainer != null && pauseContainer.Visibility == Visibility.Visible)
                    {
                        var pauseButton = pauseContainer.Child as Button;
                        if (pauseButton != null)
                        {
                            // Simuler un mouvement de souris
                            var point = new Point(pauseButton.ActualWidth / 2, pauseButton.ActualHeight / 2);
                            var args = new MouseEventArgs(Mouse.PrimaryDevice, 0)
                            {
                                RoutedEvent = Mouse.MouseMoveEvent,
                                Source = pauseButton
                            };
                            
                            // Déclencher les événements MouseEnter et MouseMove
                            pauseButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                            pauseButton.RaiseEvent(args);
                            
                            // Déclencher MouseLeave après un court délai
                            var timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(50) };
                            timer.Tick += (s, e) => {
                                pauseButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                                timer.Stop();
                            };
                            timer.Start();
                        }
                    }
                    else if (stopContainer != null && stopContainer.Visibility == Visibility.Visible)
                    {
                        var stopButton = stopContainer.Child as Button;
                        if (stopButton != null)
                        {
                            // Simuler un mouvement de souris
                            var point = new Point(stopButton.ActualWidth / 2, stopButton.ActualHeight / 2);
                            var args = new MouseEventArgs(Mouse.PrimaryDevice, 0)
                            {
                                RoutedEvent = Mouse.MouseMoveEvent,
                                Source = stopButton
                            };
                            
                            // Déclencher les événements MouseEnter et MouseMove
                            stopButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                            stopButton.RaiseEvent(args);
                            
                            // Déclencher MouseLeave après un court délai
                            var timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(50) };
                            timer.Tick += (s, e) => {
                                stopButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                                timer.Stop();
                            };
                            timer.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SimulateButtonInteraction: {ex.Message}");
            }
        }
        
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                {
                    return typedChild;
                }
                
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }
        
        private T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && typedChild.Name == name)
                {
                    return typedChild;
                }
                
                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }
    }
}