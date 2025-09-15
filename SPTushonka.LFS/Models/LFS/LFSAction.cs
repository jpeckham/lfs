using System.Text.Json.Serialization;

namespace SPTushonka.LFS.Models.LFS;

public record LFSAction
{
    [JsonPropertyName("href")]
    public required string Href { get; init; }

    [JsonPropertyName("header")]
    public Dictionary<string, string>? Header { get; init; }

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }
}
