using IconPacks.Avalonia.EvaIcons;
using Nodis.Frontend.Interfaces;
using Nodis.Frontend.ViewModels;

namespace Nodis.Frontend.Views;

public partial class MarketplacePage : ReactiveUserControl<MarketplacePageViewModel>, IMainWindowPage
{
    public string Title => "Node store";
    public PackIconEvaIconsKind Icon => PackIconEvaIconsKind.Globe;

    public MarketplacePage()
    {
        InitializeComponent();
    }
}