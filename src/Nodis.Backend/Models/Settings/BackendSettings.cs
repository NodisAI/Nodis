using System.Text.Json.Serialization;
using Nodis.Core.Models;

namespace Nodis.Backend.Models.Settings;

public class BackendSettings : SettingsBase
{

}

[JsonSerializable(typeof(BackendSettings))]
public partial class BackendOptionsJsonSerializerContext : JsonSerializerContext;