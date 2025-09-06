using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public record LFSAction
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = "";

    [JsonPropertyName("header")]
    public Dictionary<string, string>? Header { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
