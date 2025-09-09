using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models.LFS;

public record LFSBatchRequest
{
    [JsonPropertyName("operation")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LFSOperation Operation { get; set; }

    [JsonPropertyName("transfers")]
    public string[] Transfers { get; set; } = ["basic"];

    [JsonPropertyName("ref")]
    public LFSReference? Reference { get; set; }

    [JsonPropertyName("objects")]
    public LFSObject[] Objects { get; set; } = [];
}
