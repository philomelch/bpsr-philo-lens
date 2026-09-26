using System.Text.Json;

namespace ReleaseTool;

internal static class Json
{
    /// <summary>camelCase, as manifest-standard.md specifies; strict about unknown fields so a
    /// typo in stellar-plugin.json fails loudly instead of being silently dropped.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
}
