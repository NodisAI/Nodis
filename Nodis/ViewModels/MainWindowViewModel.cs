namespace Nodis.ViewModels;

public class MainWindowViewModel : ReactiveViewModelBase
{
    public string Greeting => $"Welcome to {nameof(Nodis)}!";
}