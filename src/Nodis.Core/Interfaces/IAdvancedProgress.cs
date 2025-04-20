namespace Nodis.Core.Interfaces;

/// <summary>
/// An interface that can report progress as a percentage (0-100) and as a status string.
/// </summary>
public interface IAdvancedProgress : IProgress<double>, IProgress<string>;

public static class AdvancedProgressExtension
{
    public static void Report(this IAdvancedProgress progress, double? value, string? title)
    {
        if (value is not null) progress.Report(value.Value);
        if (title is not null) progress.Report(title);
    }
}