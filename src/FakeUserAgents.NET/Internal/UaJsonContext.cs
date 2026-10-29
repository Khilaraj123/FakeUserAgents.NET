using System.Text.Json.Serialization;

namespace Dotnet.FakeUserAgents.Internal;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for deserializing <see cref="UaEntry"/> collections.
/// Enables high-performance, reflection-free JSON parsing that is Native AOT and trimming compatible.
/// </summary>
[JsonSerializable(typeof(UaEntry[]))]
internal sealed partial class UaJsonContext : JsonSerializerContext;
