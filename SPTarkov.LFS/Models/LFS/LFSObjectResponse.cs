using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public record LFSObjectResponse
{
    [JsonPropertyName("oid")]
    public string ObjectId { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("authenticated")]
    public bool? Authenticated { get; set; }

    [JsonPropertyName("actions")]
    public Dictionary<string, LFSAction>? Actions { get; set; }

    [JsonPropertyName("error")]
    public LFSError? Error { get; set; }
}
