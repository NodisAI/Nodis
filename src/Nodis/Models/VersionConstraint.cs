using System.Runtime.Serialization;

namespace Nodis.Models;

public enum VersionConstraintType
{
    Any,
    [EnumMember(Value = "==")]
    Equal,
    [EnumMember(Value = "!=")]
    NotEqual,
    [EnumMember(Value = ">")]
    GreaterThan,
    [EnumMember(Value = ">=")]
    GreaterThanOrEqual,
    [EnumMember(Value = "<")]
    LessThan,
    [EnumMember(Value = "<=")]
    LessThanOrEqual
}

public record VersionConstraint(Version Version, VersionConstraintType Type)
{
    public bool IsSatisfied(Version version)
    {
        return Type switch
        {
            VersionConstraintType.Any => true,
            VersionConstraintType.Equal => version == Version,
            VersionConstraintType.NotEqual => version != Version,
            VersionConstraintType.GreaterThan => version > Version,
            VersionConstraintType.GreaterThanOrEqual => version >= Version,
            VersionConstraintType.LessThan => version < Version,
            VersionConstraintType.LessThanOrEqual => version <= Version,
            _ => false
        };
    }
}

public record NameAndVersionConstraint(string Name, VersionConstraint VersionConstraint)
{
    /// <summary>
    /// e.g.
    /// ollama >= 0.5.12
    /// ollama==0.5.12
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static NameAndVersionConstraint Parse(string input)
    {
        var parts = input.Split([' ', '>', '<', '=', '!'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return new NameAndVersionConstraint(input, new VersionConstraint(new Version(), VersionConstraintType.Any));
        }

        var name = parts[0];
        var versionPart = input[name.Length..].Trim();
        var type = versionPart switch
        {
            _ when versionPart.StartsWith("==") => VersionConstraintType.Equal,
            _ when versionPart.StartsWith("!=") => VersionConstraintType.NotEqual,
            _ when versionPart.StartsWith(">=") => VersionConstraintType.GreaterThanOrEqual,
            _ when versionPart.StartsWith('>') => VersionConstraintType.GreaterThan,
            _ when versionPart.StartsWith("<=") => VersionConstraintType.LessThanOrEqual,
            _ when versionPart.StartsWith('<') => VersionConstraintType.LessThan,
            _ => throw new ArgumentException("Invalid version constraint type", nameof(input))
        };

        var versionString = versionPart[type.ToString().Length..];
        if (!Version.TryParse(versionString, out var version))
        {
            throw new ArgumentException("Invalid version format", nameof(input));
        }

        return new NameAndVersionConstraint(name, new VersionConstraint(version, type));
    }
}