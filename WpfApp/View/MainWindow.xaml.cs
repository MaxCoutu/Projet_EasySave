using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Animation;
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
        private DateTime _lastRenderTime = DateTime.Now; // Pour limiter les rendus
        private Random _rnd = new Random(); // Pour les micro-variations

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(App.BackupService, App.LanguageService, App.PathProvider);
            DataContext = _vm;
            
            // S'abonner à l'événement de rendu global pour forcer les rafraîchissements
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            
            // Refresh jobs when window is loaded
            this.Loaded += MainWindow_Loaded;
            
            // Refresh jobs whenever the window is activated (brought to the foreground)
            this.Activated += MainWindow_Activated;
            
            // Add handler for mouse clicks to ensure focus is cleared
            this.MouseDown += MainWindow_MouseDown;
            
            // Listen for layout updates to ensure UI properly refreshes
            this.LayoutUpdated += MainWindow_LayoutUpdated;
            
            // Add handler for all button clicks to ensure proper focus clearing
            this.AddHandler(Button.ClickEvent, new RoutedEventHandler(Button_Click), true);
            
            // Set focus management properties for the window
            System.Windows.Input.KeyboardNavigation.SetTabNavigation(this, System.Windows.Input.KeyboardNavigationMode.None);
            System.Windows.Input.KeyboardNavigation.SetControlTabNavigation(this, System.Windows.Input.KeyboardNavigationMode.None);
        }
        
        // Méthode qui s'exécute à chaque frame rendue par WPF - utilise le rendu GPU pour forcer les rafraîchissements
        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            // Limiter la fréquence pour éviter trop de rendus inutiles (1 toutes les 100ms = 10 fps)
            if ((DateTime.Now - _lastRenderTime).TotalMilliseconds < 100) return;
            _lastRenderTime = DateTime.Now;
            
            try
            {
                // Forcer le rafraîchissement des jobs dans la vue principale
                foreach (var job in _vm.RecentJobs)
                {
                    if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                    {
                        // Forcer le compteur de rafraîchissement à s'incrémenter 
                        job.RefreshCounter++;
                        
                        // Forcer l'invalidation visuelle des ProgressBars
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                            // Trouver l'ItemsControl contenant les jobs
                            var jobsContainer = FindVisualChild<ItemsControl>(this, "RecentJobsItemsControl");
                            if (jobsContainer != null)
                            {
                                // Trouver le conteneur correspondant au job
                                var item = jobsContainer.ItemContainerGenerator.ContainerFromItem(job) as FrameworkElement;
                                if (item != null)
                                {
                                    // Trouver la ProgressBar
                                    var progressBar = FindVisualChild<ProgressBar>(item, "JobProgressBar");
                                    if (progressBar != null)
                                    {
                                        // Forcer le rafraîchissement visuel
                                        ForceProgressBarUpdate(progressBar, job.Progression / 100.0);
                                        
                                        // Force tous les éléments parents à se rafraîchir également
                                        InvalidateParentVisuals(progressBar);
                                    }
                                }
                            }
                        }), DispatcherPriority.Render);
                    }
                }
                
                // Force un rafraîchissement global pour les données
                if (_vm.RecentJobs.Count > 0)
                {
                    // Toutes les 500ms (5 frames à 10fps), force une relecture complète des données
                    if (_rnd.Next(0, 5) == 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                            _vm.ForceRefreshJobs();
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
        
        // Méthode utilitaire pour trouver un enfant visuel d'un type spécifique
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
        
        // Méthode utilitaire pour trouver un enfant visuel par nom
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
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Clear focus immediately after any button click
            Console.WriteLine("Button clicked - clearing focus");
            Keyboard.ClearFocus();
            this.Focus();
            
            // Force a more aggressive focus clearing
            FocusManager.SetFocusedElement(this, null);
            
            // Get the clicked button
            if (sender is Button clickedButton)
            {
                Console.WriteLine($"Button clicked: {clickedButton.ToolTip}");
                
                // Force a refresh immediately
                _vm.ForceRefreshJobs();
                
                // And then after a short delay
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(100);
                timer.Tick += (s, args) => {
                    _vm.ForceRefreshJobs();
                    timer.Stop();
                };
                timer.Start();
            }
            else
            {
                // Force a refresh of the UI after a short delay
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(50);
                timer.Tick += (s, args) => {
                    _vm.ForceRefreshJobs();
                    timer.Stop();
                };
                timer.Start();
            }
        }
        
        // Special handler for job control buttons (pause/resume/stop)
        private void ControlButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[CRITICAL] Job control button clicked!");
            
            // Identify which button was clicked
            if (sender is Button clickedButton)
            {
                Console.WriteLine($"[CRITICAL] Button type: {clickedButton.ToolTip}, Tag: {clickedButton.Tag}");
                
                // If this is a stop button, we need special handling to ensure play button becomes visible
                if (clickedButton.ToolTip.ToString().Contains("Stop"))
                {
                    Console.WriteLine("[CRITICAL] STOP BUTTON detected - ensuring play button will appear");
                    
                    // The stop command will be executed after this handler
                    // Schedule a series of refreshes to make sure UI updates correctly
                    ScheduleMultipleRefreshes();
                }
                else if (clickedButton.ToolTip.ToString().Contains("Pause"))
                {
                    Console.WriteLine("[CRITICAL] PAUSE/RESUME BUTTON detected");
                    
                    // For pause/resume, we also want multiple refreshes
                    ScheduleMultipleRefreshes();
                }
            }
            
            // Clear focus immediately
            Keyboard.ClearFocus();
            this.Focus();
            FocusManager.SetFocusedElement(this, null);
            
            // Force immediate UI update
            _vm.ForceRefreshJobs();
        }
        
        // Helper method to schedule multiple refreshes with increasing delays
        private void ScheduleMultipleRefreshes()
        {
            // First refresh after 50ms
            var timer1 = new DispatcherTimer();
            timer1.Interval = TimeSpan.FromMilliseconds(50);
            timer1.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing first post-control refresh");
                _vm.ForceRefreshJobs();
                timer1.Stop();
            };
            timer1.Start();
            
            // Second refresh after 150ms
            var timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromMilliseconds(150);
            timer2.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing second post-control refresh");
                _vm.ForceRefreshJobs();
                timer2.Stop();
            };
            timer2.Start();
            
            // Third refresh after 300ms to ensure everything is settled
            var timer3 = new DispatcherTimer();
            timer3.Interval = TimeSpan.FromMilliseconds(300);
            timer3.Tick += (s, args) => {
                Console.WriteLine("[CRITICAL] Performing final post-control refresh");
                _vm.ForceRefreshJobs();
                timer3.Stop();
            };
            timer3.Start();
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure jobs are refreshed when the window is loaded
            _vm.RefreshJobs();
            
            // Force loading of all jobs into RecentJobs
            var allJobs = App.BackupService.GetJobs();
            if (allJobs.Count > 0)
            {
                _vm.RecentJobs.Clear();
                foreach (var job in allJobs)
                {
                    _vm.RecentJobs.Add(job);
                }
                
                // Update job status
                typeof(MainViewModel).GetMethod("UpdateJobStatusInternal", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance)?.Invoke(_vm, null);
            }
        }
        
        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // Use the new ForceRefreshJobs method
            Console.WriteLine("MainWindow activated - using ForceRefreshJobs method");
            _vm.ForceRefreshJobs();
        }
        
        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clear focus when clicking anywhere in the window
            Keyboard.ClearFocus();
            this.Focus();
            
            // Force a more aggressive focus clearing
            FocusManager.SetFocusedElement(this, null);
        }
        
        // Track layout updates
        private DateTime _lastLayoutUpdate = DateTime.MinValue;
        
        private void MainWindow_LayoutUpdated(object sender, EventArgs e)
        {
            // Only refresh at most once per second to avoid performance issues
            if ((DateTime.Now - _lastLayoutUpdate).TotalSeconds >= 1)
            {
                _lastLayoutUpdate = DateTime.Now;
                // Only refresh if we're in the main view (not showing a dialog)
                if (_vm.CurrentViewModel == _vm)
                {
                    Console.WriteLine("Layout updated - refreshing jobs");
                    _vm.ForceRefreshJobs();
                }
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Se désabonner de l'événement de rendu lorsque la fenêtre est fermée
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            base.OnClosed(e);
        }
    }
}