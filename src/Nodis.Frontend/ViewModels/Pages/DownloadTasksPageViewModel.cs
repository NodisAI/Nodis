using System.Diagnostics.CodeAnalysis;
using ObservableCollections;

namespace Nodis.Frontend.ViewModels;

public class DownloadTasksPageViewModel : ReactiveViewModelBase, IDownloadTasksManager
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<DownloadTask> DownloadTasks =>
        field ??= downloadTasks.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<DownloadTask> downloadTasks = [];

    public void Add(DownloadTask task) => downloadTasks.Add(task);

    public void Remove(DownloadTask task) => downloadTasks.Remove(task);
}