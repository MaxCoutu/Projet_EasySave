using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{

    public partial class MainWindow : Window
    {
        private readonly MonitorViewModel _vm = new MonitorViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            Loaded += async (s, e) => await _vm.LoadJobsAsync();
        }
    }
}