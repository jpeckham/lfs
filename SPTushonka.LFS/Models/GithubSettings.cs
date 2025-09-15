namespace SPTushonka.LFS.Models;

public record GithubSettings
{
    public required string Organisation { get; init; }
    public required string Repository { get; init; }
}
