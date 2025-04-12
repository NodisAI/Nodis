using System.Runtime.Serialization;
using System.Security.Cryptography;
using Nodis.Core.Extensions;
using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Nodis.Core.Models;

// ReSharper disable InconsistentNaming
public enum ChecksumType
{
    [EnumMember(Value = "md5")]
    MD5,
    [EnumMember(Value = "sha1")]
    SHA1,
    [EnumMember(Value = "sha256")]
    SHA256,
    [EnumMember(Value = "sha512")]
    SHA512
}
// ReSharper restore InconsistentNaming

[YamlObject]
public partial record Checksum(ChecksumType Type, string Value)
{
    public HashAlgorithm CreateHashAlgorithm()
    {
        return Type switch
        {
            ChecksumType.MD5 => MD5.Create(),
            ChecksumType.SHA1 => SHA1.Create(),
            ChecksumType.SHA256 => SHA256.Create(),
            ChecksumType.SHA512 => SHA512.Create(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public class ChecksumYamlFormatter : IYamlFormatter<Checksum>
{
    public void Serialize(ref Utf8YamlEmitter emitter, Checksum value, YamlSerializationContext context)
    {
        emitter.Tag('!' + value.Type.ToFriendlyString());
        context.Serialize(ref emitter, value.Value);
    }

    public Checksum Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        ChecksumType type;
        if (!parser.TryGetCurrentTag(out var tag)) type = ChecksumType.MD5;
        else type = tag.Handle.ToEnum<ChecksumType>();
        var value = context.DeserializeWithAlias<string>(ref parser);
        return new Checksum(type, value);
    }
}