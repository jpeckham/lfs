using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public record LFSObject
{
    [JsonPropertyName("oid")]
    public required string ObjectId { get; init; }

    [JsonPropertyName("size")]
    public required long Size { get; init; }
}
