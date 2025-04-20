using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using MessagePack;
using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Nodis.Core.Models;

[YamlObject]
[MessagePackObject]
public partial record struct SemanticVersion : IComparable<SemanticVersion>
{
    public static IReadOnlyList<string> SpecialVersions => SpecialVersionsArray;
    private static readonly string[] SpecialVersionsArray = ["latest", "prerelease", "stable"];

    [YamlMember("major")] [Key(0)] public int Major { get; init; }
    [YamlMember("minor")] [Key(1)] public int Minor { get; init; }
    [YamlMember("patch")] [Key(2)] public int Patch { get; init; }
    [YamlMember("prerelease")] [Key(3)] public string? Prerelease { get; init; }
    [YamlMember("build")] [Key(4)] public string? Build { get; init; }
    [YamlMember("special")] [Key(5)] public string? Special { get; init; }

    [YamlIgnore]
    [IgnoreMember]
    [MemberNotNullWhen(true, nameof(Special))]
    public bool IsSpecial => Special != null;

    [YamlConstructor]
    public SemanticVersion() { }

    public SemanticVersion(int major, int minor, int patch, string? prerelease, string? build)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        Build = build;
    }

    public SemanticVersion(string special)
    {
        Special = special.ToLowerInvariant();
        if (!SpecialVersions.Contains(Special))
            throw new FormatException("Invalid special version");
    }

    public static SemanticVersion Parse(string input)
    {
        if (TryParse(input, out var version))
            return version;
        throw new FormatException("Invalid version format");
    }

    public static bool TryParse(string input, out SemanticVersion version)
    {
        return TryParseSpecial(input, out version) || TryParseSemantic(input, out version);
    }

    private static bool TryParseSpecial(string input, out SemanticVersion version)
    {
        var lowerInput = input.ToLowerInvariant();
        if (SpecialVersions.Contains(lowerInput))
        {
            version = new SemanticVersion(lowerInput);
            return true;
        }
        version = default;
        return false;
    }

    private static bool TryParseSemantic(string input, out SemanticVersion version)
    {
        var match = VersionRegex().Match(input);
        if (!match.Success)
        {
            version = default;
            return false;
        }

        version = new SemanticVersion(
            major: int.Parse(match.Groups["major"].Value),
            minor: int.Parse(match.Groups["minor"].Value),
            patch: int.Parse(match.Groups["patch"].Value),
            prerelease: match.Groups["prerelease"].Value,
            build: match.Groups["build"].Value
        );
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        if (IsSpecial != other.IsSpecial)
            throw new InvalidOperationException("Cannot compare different version types");

        return IsSpecial ? CompareSpecial(other) : CompareSemantic(other);
    }

    public override string ToString()
    {
        if (IsSpecial) return Special;
        var sb = new StringBuilder();
        sb.Append(Major).Append('.').Append(Minor).Append('.').Append(Patch);
        if (!string.IsNullOrEmpty(Prerelease)) sb.Append('-').Append(Prerelease);
        if (!string.IsNullOrEmpty(Build)) sb.Append('+').Append(Build);
        return sb.ToString();
    }

    #region operators

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    #endregion

    private int CompareSemantic(SemanticVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        return ComparePrerelease(Prerelease, other.Prerelease);
    }

    private static int ComparePrerelease(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : 1;
        if (string.IsNullOrEmpty(b)) return -1;

        var aParts = a.Split('.');
        var bParts = b.Split('.');

        for (var i = 0; i < Math.Max(aParts.Length, bParts.Length); i++)
        {
            if (i >= aParts.Length) return -1;
            if (i >= bParts.Length) return 1;

            if (int.TryParse(aParts[i], out var aNum) &&
                int.TryParse(bParts[i], out var bNum))
            {
                var numCompare = aNum.CompareTo(bNum);
                if (numCompare != 0) return numCompare;
            }
            else
            {
                var strCompare = string.Compare(
                    aParts[i],
                    bParts[i],
                    StringComparison.Ordinal);
                if (strCompare != 0) return strCompare;
            }
        }
        return 0;
    }

    private int CompareSpecial(SemanticVersion other)
    {
        var aIndex = Array.IndexOf(SpecialVersionsArray, Special);
        var bIndex = Array.IndexOf(SpecialVersionsArray, other.Special);
        return bIndex.CompareTo(aIndex);
    }

    [GeneratedRegex(
        @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(-(?<prerelease>[0-9A-Za-z\-\.]+))?(\+(?<build>[0-9A-Za-z\-\.]+))?$",
        RegexOptions.Compiled)]
    private static partial Regex VersionRegex();
}

public enum VersionOperator
{
    Any,
    /// <summary>
    /// ==
    /// </summary>
    Equal,
    /// <summary>
    /// !=
    /// </summary>
    NotEqual,
    /// <summary>
    /// >
    /// </summary>
    GreaterThan,
    /// <summary>
    /// >=
    /// </summary>
    GreaterThanOrEqual,
    /// <summary>
    /// &lt;
    /// </summary>
    LessThan,
    /// <summary>
    /// &lt;=
    /// </summary>
    LessThanOrEqual,
    /// <summary>
    /// ~=
    /// </summary>
    Compatible
}

public readonly record struct VersionConstraint(VersionOperator Operator, SemanticVersion Version)
{
    public bool IsSatisfied(SemanticVersion other)
    {
        if (Operator == VersionOperator.Any)
            return true;

        if (other.IsSpecial != Version.IsSpecial)
            return false;

        var comparison = other.CompareTo(Version);
        return Operator switch
        {
            VersionOperator.Equal => comparison == 0,
            VersionOperator.NotEqual => comparison != 0,
            VersionOperator.GreaterThan => comparison > 0,
            VersionOperator.GreaterThanOrEqual => comparison >= 0,
            VersionOperator.LessThan => comparison < 0,
            VersionOperator.LessThanOrEqual => comparison <= 0,
            VersionOperator.Compatible => CheckCompatible(other),
            _ => false
        };
    }

    private bool CheckCompatible(SemanticVersion other)
    {
        return other >= Version && other < new SemanticVersion(Version.Major + 1, 0, 0, null, null);
    }

    public override string ToString()
    {
        return Operator switch
        {
            VersionOperator.Equal => $"=={Version}",
            VersionOperator.NotEqual => $"!={Version}",
            VersionOperator.GreaterThan => $">{Version}",
            VersionOperator.GreaterThanOrEqual => $">={Version}",
            VersionOperator.LessThan => $"<{Version}",
            VersionOperator.LessThanOrEqual => $"<={Version}",
            VersionOperator.Compatible => $"~={Version}",
            _ => string.Empty
        };
    }
}

public readonly partial record struct NameAndVersionConstraints(string Name, VersionConstraint[] Constraints)
{
    public bool IsSatisfied(SemanticVersion version)
    {
        return Constraints.All(p => p.IsSatisfied(version));
    }

    public override string ToString()
    {
        return $"{Name}{string.Join(',', Constraints)}";
    }

    public static NameAndVersionConstraints Parse(string input)
    {
        if (!TryParse(input, out var result))
            throw new FormatException("Invalid constraints format");
        return result;
    }

    public static bool TryParse(string input, out NameAndVersionConstraints result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var match = ParserRegex().Match(input);
        if (!match.Success)
            return false;

        var name = match.Groups["name"].Value.Trim();
        if (string.IsNullOrEmpty(name))
            return false;

        var constraints = new List<VersionConstraint>();
        var constraintsStr = match.Groups["constraints"].Value;

        if (!string.IsNullOrWhiteSpace(constraintsStr))
        {
            var constraintParts = constraintsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in constraintParts)
            {
                var partMatch = ConstraintRegex().Match(part.Trim());
                if (!partMatch.Success)
                    return false;

                var opValue = partMatch.Groups["op"].Value;
                var versionValue = partMatch.Groups["version"].Value.Trim();
                if (!SemanticVersion.TryParse(versionValue, out var version))
                    return false;

                var op = ParseOperator(opValue);
                constraints.Add(new VersionConstraint(op, version));
            }
        }
        else
        {
            constraints.Add(new VersionConstraint(VersionOperator.Any, default));
        }

        result = new NameAndVersionConstraints(name, constraints.Distinct().ToArray());
        return true;
    }

    private static VersionOperator ParseOperator(string op)
    {
        return op switch
        {
            "==" => VersionOperator.Equal,
            "!=" => VersionOperator.NotEqual,
            ">" => VersionOperator.GreaterThan,
            ">=" => VersionOperator.GreaterThanOrEqual,
            "<" => VersionOperator.LessThan,
            "<=" => VersionOperator.LessThanOrEqual,
            "~=" => VersionOperator.Compatible,
            "" => VersionOperator.Equal,
            _ => VersionOperator.Any
        };
    }

    [GeneratedRegex(
        @"^\s*(?<name>[^,<=>!~]+?(?=\s*([,<=>!~]|$)))\s*(?<constraints>.*)$",
        RegexOptions.Compiled)]
    private static partial Regex ParserRegex();

    [GeneratedRegex(
        @"^\s*(?<op><=|>=|==|=|<|>|!=|~=)?\s*(?<version>.+?)\s*$",
        RegexOptions.Compiled)]
    private static partial Regex ConstraintRegex();
}

public class NameAndVersionConstraintsYamlFormatter : IYamlFormatter<NameAndVersionConstraints>
{
    public void Serialize(ref Utf8YamlEmitter emitter, in NameAndVersionConstraints value, YamlSerializationContext context)
    {
        context.Serialize(ref emitter, value.ToString());
    }

    public NameAndVersionConstraints Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        return NameAndVersionConstraints.Parse(context.DeserializeWithAlias<string>(ref parser));
    }
}