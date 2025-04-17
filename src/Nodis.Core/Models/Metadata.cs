namespace Nodis.Core.Models;

/// <param name="Namespace">e.g. NodisAI.Main</param>
/// <param name="Name">e.g. ollama</param>
/// <param name="Version">e.g. 0.5.12</param>
public record struct Metadata(string Namespace, string Name, SemanticVersion Version) : IComparable<Metadata>
{
    public int CompareTo(Metadata other)
    {
        var namespaceComparison = string.Compare(Namespace, other.Namespace, StringComparison.OrdinalIgnoreCase);
        if (namespaceComparison != 0) return namespaceComparison;
        var nameComparison = string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        if (nameComparison != 0) return nameComparison;
        return Version.CompareTo(other.Version);
    }

    public override string ToString() => $"{Namespace}.{Name} ({Version})";

    public override int GetHashCode() => HashCode.Combine(
        Namespace.GetHashCode(StringComparison.OrdinalIgnoreCase),
        Name.GetHashCode(StringComparison.OrdinalIgnoreCase),
        Version.GetHashCode());

    public bool Equals(Metadata? other)
    {
        if (other is null) return false;
        return Namespace.Equals(other.Value.Namespace, StringComparison.OrdinalIgnoreCase) &&
               Name.Equals(other.Value.Name, StringComparison.OrdinalIgnoreCase) &&
               Version.Equals(other.Value.Version);
    }
}