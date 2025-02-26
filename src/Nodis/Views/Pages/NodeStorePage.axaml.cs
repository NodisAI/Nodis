using IconPacks.Avalonia.EvaIcons;
using Nodis.Interfaces;
using Nodis.ViewModels;

namespace Nodis.Views;

public partial class NodeStorePage : ReactiveUserControl<NodeStorePageViewModel>, IMainWindowPage
{
    public string Title => "Node store";
    public PackIconEvaIconsKind Icon => PackIconEvaIconsKind.Globe;

    public NodeStorePage()
    {
        InitializeComponent();
    }
}