using Nodis.Core.Models;

namespace Nodis.Core.Interfaces;

public interface IDownloadTasksManager
{
    void Add(DownloadTask task);

    void Remove(DownloadTask task);
}