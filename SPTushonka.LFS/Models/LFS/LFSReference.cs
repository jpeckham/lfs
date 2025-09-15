using System.Text.Json.Serialization;

namespace SPTushonka.LFS.Models.LFS;

public record LFSReference
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
