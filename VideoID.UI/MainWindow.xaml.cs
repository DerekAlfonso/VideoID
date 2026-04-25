using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using VideoID.UI.ViewModels;

namespace VideoID.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
