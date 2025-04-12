namespace Nodis.Core.Models;

public enum ProcessStartType
{
    /// <summary>
    /// Use Process
    /// </summary>
    Normal,
    /// <summary>
    /// Use bash
    /// </summary>
    Bash
}

public record ProcessCreationOptions
{
    public ProcessStartType Type { get; init; }

    /// <summary>
    /// When use <see cref="ProcessStartType.Bash"/>, this can be the script (*.sh) to execute
    /// </summary>
    public string? Executable { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>
    /// When current process exits, kill the started process.
    /// </summary>
    public bool KillOnExit { get; init; } = true;
}