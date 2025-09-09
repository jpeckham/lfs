using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public record LFSBatchResponse
{
    [JsonPropertyName("transfer")]
    public string Transfer { get; set; } = "basic";

    [JsonPropertyName("objects")]
    public List<LFSObjectResponse> Objects { get; set; } = [];
}
