using IconPacks.Avalonia.EvaIcons;
using Nodis.Interfaces;
using Nodis.ViewModels;

namespace Nodis.Views;

public partial class WorkflowEditPage : ReactiveUserControl<WorkflowEditPageViewModel>, IMainWindowPage
{
    public string Title => "Workflow";
    public PackIconEvaIconsKind Icon => PackIconEvaIconsKind.Layers;

    public WorkflowEditPage()
    {
        InitializeComponent();
    }
}