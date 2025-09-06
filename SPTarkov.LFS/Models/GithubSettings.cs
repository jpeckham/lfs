namespace SPTarkov.LFS.Models;

public record GithubSettings
{
    public string Organisation { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
}
