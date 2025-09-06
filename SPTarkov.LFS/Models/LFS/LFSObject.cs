using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public class LFSObject
{
    [JsonPropertyName("oid")]
    public string ObjectId { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
