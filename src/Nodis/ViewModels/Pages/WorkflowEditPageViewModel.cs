using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using IconPacks.Avalonia.EvaIcons;
using Nodis.Extensions;
using Nodis.Interfaces;
using Nodis.Models.Workflow;
using ObservableCollections;

namespace Nodis.ViewModels;

public partial class WorkflowEditPageViewModel(
    IEnvironmentManager environmentManager
) : ReactiveViewModelBase
{
    [ObservableProperty]
    public partial WorkflowContext? WorkflowContext { get; set; } = new();

    [field: AllowNull, MaybeNull]
    internal NotifyCollectionChangedSynchronizedViewList<NodeGroup> NodeGroups =>
        field ??= nodeGroups.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<NodeGroup> nodeGroups = [];

    protected internal override async Task ViewLoaded(CancellationToken cancellationToken)
    {
        nodeGroups.Clear();

        nodeGroups.Add(new NodeGroup("BuiltIn",
        [
            new NodeTemplate("Condition", PackIconEvaIconsKind.CheckmarkCircle, () => new WorkflowConditionNode()),
            new NodeTemplate("Constant", PackIconEvaIconsKind.FileText, () => new WorkflowConstantNode()),
            new NodeTemplate("Delay", PackIconEvaIconsKind.Clock, () => new WorkflowDelayNode()),
            new NodeTemplate("Display", PackIconEvaIconsKind.Browser, () => new WorkflowDisplayNode()),
            new NodeTemplate("Loop", PackIconEvaIconsKind.Refresh, () => new WorkflowLoopNode()),
        ]));

        foreach (var (@namespace, items) in environmentManager.EnumerateNodes().GroupBy(m => m.Namespace).Select(g => (g.Key, g)))
        {
            nodeGroups.Add(new NodeGroup(
                @namespace,
                await items
                    .SelectAsync(m => environmentManager.LoadNodeAsync(m, cancellationToken))
                    .SelectManyAsync(m => m.Nodes)
                    .SelectAsync(
                        n =>
                        {
                            n.Namespace = @namespace;
                            return new NodeTemplate(n.Name, PackIconEvaIconsKind.None, n.Clone);
                        })
                    .ToListAsync()));
        }

        await base.ViewLoaded(cancellationToken);
    }

    internal record NodeGroup(string Name, IList<NodeTemplate> Items);

    internal record NodeTemplate(string Name, PackIconEvaIconsKind Icon, Func<WorkflowNode> NodeFactory);
}