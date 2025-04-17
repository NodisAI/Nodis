using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using IconPacks.Avalonia.EvaIcons;
using ObservableCollections;

namespace Nodis.Frontend.ViewModels;

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
            new NodeTemplate("Condition", PackIconEvaIconsKind.CheckmarkCircle, () => new ConditionNode()),
            new NodeTemplate("Delay", PackIconEvaIconsKind.Clock, () => new DelayNode()),
            new NodeTemplate("File", PackIconEvaIconsKind.File, () => new FileNode()),
            new NodeTemplate("HTTP Request", PackIconEvaIconsKind.Globe, () => new HttpRequestNode()),
            new NodeTemplate("Loop", PackIconEvaIconsKind.Refresh, () => new LoopNode()),
            new NodeTemplate("Preview", PackIconEvaIconsKind.Eye, () => new PreviewNode()),
            new NodeTemplate("Serializer", PackIconEvaIconsKind.FileText, () => new SerializerNode()),
            new NodeTemplate("Trigger", PackIconEvaIconsKind.Bell, () => new TriggerNode()),
            new NodeTemplate("Variable", PackIconEvaIconsKind.Hash, () => new VariableNode()),
        ]));

        var packages = await environmentManager.EnumerateInstalledBundleMetadataAsync();
        foreach (var (@namespace, items) in packages.GroupBy(m => m.Namespace).Select(g => (g.Key, g)))
        {
            nodeGroups.Add(new NodeGroup(
                @namespace,
                await items
                    .ToAsyncEnumerable()
                    .SelectAwait(m => environmentManager.LoadInstalledBundleAsync(m, cancellationToken).ToValueTask())
                    .SelectMany(m => m.Nodes.ToAsyncEnumerable())
                    .Select(
                        n =>
                        {
                            n.Namespace = @namespace;
                            return new NodeTemplate(n.Name, PackIconEvaIconsKind.None, n.Clone);
                        })
                    .ToListAsync(cancellationToken: cancellationToken)));
        }

        await base.ViewLoaded(cancellationToken);
    }

    internal record NodeGroup(string Name, IList<NodeTemplate> Items);

    internal record NodeTemplate(string Name, PackIconEvaIconsKind Icon, Func<Node> NodeFactory);
}