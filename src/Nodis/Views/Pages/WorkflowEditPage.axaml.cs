using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private void HandleAddNodeButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WorkflowEditPageViewModel.NodeTemplate nodeTemplate }) return;
        if (ViewModel.WorkflowContext is not { } workflowContext) return;
        var node = nodeTemplate.NodeFactory();
        var viewport = WorkflowEditor.Viewport;
        node.X = viewport.X + viewport.Width / 2;
        node.Y = viewport.Y + viewport.Height / 2;
        workflowContext.AddNode(node);
    }
}