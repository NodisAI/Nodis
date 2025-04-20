namespace Nodis.Core.Interfaces;

/// <summary>
/// An interface that can report progress as a percentage (0-100) and as a status string.
/// </summary>
public interface IAdvancedProgress : IProgress<double>, IProgress<string>
{
    void Advance(double value);
}
