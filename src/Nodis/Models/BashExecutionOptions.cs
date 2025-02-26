namespace Nodis.Models;

public record BashExecutionOptions
{
    public required IReadOnlyList<string> CommandLines { get; init; }

    public string? WorkingDirectory { get; init; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new();
}