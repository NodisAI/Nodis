namespace Nodis.Models;

/// <param name="Namespace">e.g. NodisAI.Main</param>
/// <param name="Name">e.g. ollama</param>
/// <param name="Version">e.g. 0.5.12</param>
public record Metadata(string Namespace, string Name, Version Version);