namespace Nodis.Models;

public record BashExecutionOptions
{
    public string? ScriptPath { get; init; }

    public IReadOnlyList<string> CommandLines { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new();
}