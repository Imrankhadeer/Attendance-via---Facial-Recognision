using System.Windows;
using FaceAttendance.UI.ViewModels;

namespace FaceAttendance.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
