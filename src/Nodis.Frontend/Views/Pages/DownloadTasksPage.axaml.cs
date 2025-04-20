using IconPacks.Avalonia.EvaIcons;
using Nodis.Frontend.Interfaces;
using Nodis.Frontend.ViewModels;

namespace Nodis.Frontend.Views;

public partial class DownloadTasksPage : ReactiveUserControl<DownloadTasksPageViewModel>, IMainWindowPage
{
    public string Title => "Download tasks";
    public PackIconEvaIconsKind Icon => PackIconEvaIconsKind.Download;

    public DownloadTasksPage()
    {
        InitializeComponent();
    }
}