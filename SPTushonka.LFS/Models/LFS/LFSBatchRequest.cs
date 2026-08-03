using System.Text.Json.Serialization;
using SPTushonka.LFS.Converters;

namespace SPTushonka.LFS.Models.LFS;

public record LFSBatchRequest
{
    [JsonPropertyName("operation")]
    [JsonConverter(typeof(LFSOperationConverter))]
    public LFSOperation Operation { get; set; }

    [JsonPropertyName("transfers")]
    public string[] Transfers { get; set; } = ["basic"];

    [JsonPropertyName("ref")]
    public LFSReference? Reference { get; set; }

    [JsonPropertyName("objects")]
    public LFSObject[] Objects { get; set; } = [];
}
