using System.Text.Json.Serialization;

namespace SPTushonka.LFS.Models;

public record Config
{
    [JsonPropertyName("Endpoints")]
    public required List<EndpointConfig> Endpoints { get; set; }

    [JsonPropertyName("Options")]
    public required LFSApplicationOptions Options { get; set; }
}

public record EndpointConfig
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

    [JsonIgnore]
    public GithubSettings? GithubSettings { get; set; }
}

public record LFSApplicationOptions
{
    /// <summary>
    /// Addresses or CIDR ranges of reverse proxies allowed to set X-Forwarded-For.
    /// Leaving this empty trusts forwarded headers from any source, which lets clients
    /// spoof their address and bypass rate limiting.
    /// </summary>
    public string[] TrustedProxies { get; set; } = [];

    /// <summary>
    /// Largest object accepted by the upload endpoint. Zero or negative means unlimited.
    /// </summary>
    public long MaxUploadSizeBytes { get; set; }

    /// <summary>
    /// How long a GitHub permission lookup is reused before re-querying the API.
    /// Zero disables caching, at the cost of an API call per object per request.
    /// </summary>
    public int GithubAuthCacheSeconds { get; set; } = 300;

    public RateLimitOptions RateLimit { get; set; } = new();
}

public record RateLimitOptions
{
    public int PermitLimit { get; set; } = 10000;

    public int WindowHours { get; set; } = 24;

    /// <summary>
    /// Requests held once the window is exhausted. Anything above zero makes callers wait
    /// out the remainder of the window instead of receiving a 429.
    /// </summary>
    public int QueueLimit { get; set; }
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
