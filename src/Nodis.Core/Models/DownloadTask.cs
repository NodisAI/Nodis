using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Nodis.Core.Interfaces;

namespace Nodis.Core.Models;

public enum DownloadTaskStatus
{
    Queued,
    InProgress,
    Completed,
    Failed,
    Canceled
}

public partial class DownloadTask(string title) : ObservableObject, IAdvancedProgress
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    public partial DownloadTaskStatus Status { get; set; } = DownloadTaskStatus.Queued;

    [ObservableProperty]
    public partial object? Icon { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = title;

    /// <summary>
    /// A 0-100% progress value.
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; } = double.NaN;

    public void Report(double value) => Progress = value;

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    public void Report(string value) => ProgressText = value;

    public void Advance(double value) => Progress += value;

    [ObservableProperty]
    public partial ICommand? RetryCommand { get; set; }

    public bool CanRetry => Status is DownloadTaskStatus.Failed or DownloadTaskStatus.Canceled;

    [ObservableProperty]
    public partial ICommand? DeleteCommand { get; set; }
}