using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public record LFSReference
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
