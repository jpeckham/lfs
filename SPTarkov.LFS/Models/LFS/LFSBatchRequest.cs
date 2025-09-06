using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public record LFSBatchRequest
{
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("transfers")]
    public string[] Transfers { get; set; } = ["basic"];

    [JsonPropertyName("ref")]
    public LFSReference? Reference { get; set; }

    [JsonPropertyName("objects")]
    public LFSObject[] Objects { get; set; } = [];
}
