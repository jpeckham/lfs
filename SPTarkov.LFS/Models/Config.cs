using System.Text.Json.Serialization;

namespace SPTarkov.LFS.Models;

public record Config
{
    [JsonPropertyName("RepositoryName")]
    public required string Name { get; init; }

    [JsonPropertyName("RepositoryUrl")]
    public required string Url { get; init; }

    [JsonPropertyName("Endpoint")]
    public required string Endpoint { get; init; }

    [JsonPropertyName("RepositoryVisibility")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required EVisibility Visibility { get; init; }

    [JsonPropertyName("Authentication")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required EAuthentication Authentication { get; init; }
}

public enum EAuthentication
{
    None = 0,
    Local = 1,
    Github = 2,
}

public enum EVisibility
{
    Public = 0,
    Private = 1,
}
