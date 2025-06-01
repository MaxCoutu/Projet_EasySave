using System.Windows.Controls;
using System.Windows;
using Microsoft.Win32;
using Projet.ViewModel;
using System.IO;

namespace Projet.Wpf.View
{
    public partial class AddJobView : System.Windows.Controls.UserControl
    {
        public AddJobView()
        {
            InitializeComponent();
        }
        
        private void BrowseSourceDir_Click(object sender, RoutedEventArgs e)
        {
            // For WPF in .NET Core, we can use OpenFileDialog with a workaround
            var dialog = new OpenFileDialog
            {
                Title = "Select Source Directory",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder" // This is just a placeholder
            };
            
            // Set initial directory if there's already a path
            if (DataContext is AddJobViewModel viewModel && !string.IsNullOrEmpty(viewModel.Builder.SourceDir))
            {
                if (Directory.Exists(viewModel.Builder.SourceDir))
                {
                    dialog.InitialDirectory = viewModel.Builder.SourceDir;
                }
            }
            
            // This helps to select folders instead of files
            dialog.ValidateNames = false;
            dialog.Filter = "Folders|*.none";
            
            if (dialog.ShowDialog() == true)
            {
                // Get the selected folder path (remove the filename part)
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    // Update the SourceDir property
                    if (DataContext is AddJobViewModel vm)
                    {
                        vm.Builder.SourceDir = folderPath;
                    }
                }
            }
        }
        
        private void BrowseTargetDir_Click(object sender, RoutedEventArgs e)
        {
            // For WPF in .NET Core, we can use OpenFileDialog with a workaround
            var dialog = new OpenFileDialog
            {
                Title = "Select Target Directory",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder" // This is just a placeholder
            };
            
            // Set initial directory if there's already a path
            if (DataContext is AddJobViewModel viewModel && !string.IsNullOrEmpty(viewModel.Builder.TargetDir))
            {
                if (Directory.Exists(viewModel.Builder.TargetDir))
                {
                    dialog.InitialDirectory = viewModel.Builder.TargetDir;
                }
            }
            
            // This helps to select folders instead of files
            dialog.ValidateNames = false;
            dialog.Filter = "Folders|*.none";
            
            if (dialog.ShowDialog() == true)
            {
                // Get the selected folder path (remove the filename part)
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    // Update the TargetDir property
                    if (DataContext is AddJobViewModel vm)
                    {
                        vm.Builder.TargetDir = folderPath;
                    }
                }
            }
        }
    }
}