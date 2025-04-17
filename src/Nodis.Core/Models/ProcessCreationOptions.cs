namespace Nodis.Core.Models;

public abstract record ProcessCreationOptions
{
    public string? WorkingDirectory { get; init; }

    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();

    /// <summary>
    /// When current process exits, kill the started process.
    /// </summary>
    public bool KillOnExit { get; init; } = true;
}

public record NormalProcessCreationOptions : ProcessCreationOptions
{
    public required string Command { get; init; }

    public IReadOnlyList<string>? Arguments { get; init; }
}

public record BashProcessCreationOptions : ProcessCreationOptions
{
    public required IReadOnlyList<string> CommandLines { get; init; }

    /// <summary>
    /// Auto exit the bash process when the command is finished.
    /// </summary>
    public bool AutoExit { get; init; } = true;
}