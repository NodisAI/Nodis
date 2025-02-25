using Avalonia.Controls;
using Nodis.ViewModels;

namespace Nodis.Views;

public partial class MainWindow : Window, IReactiveView<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}