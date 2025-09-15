using System.Text.Json.Serialization;

namespace SPTushonka.LFS.Models.LFS;

public record LFSError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
