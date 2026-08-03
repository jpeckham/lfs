using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Octokit;
using SPTarkov.DI.Annotations;
using SPTushonka.LFS.Extensions;
using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;

namespace SPTushonka.LFS.Authenticators;

[Injectable]
public class GithubAuthenticator(ILogger<GithubAuthenticator> logger, IMemoryCache cache, Config appConfig) : IAuthenticator
{
    private static readonly ProductHeaderValue _headerValue = new("SPTushonka.LFS", Program.AppVersion);
    private static readonly Uri _githubUri = new("https://api.github.com/");

    public EAuthentication AuthenticationType
    {
        get { return EAuthentication.Github; }
    }

    public async Task<bool> AuthenticateAsync(EndpointConfig config, HttpContext context, LFSOperation operation)
    {
        if (config.Visibility is EVisibility.Public && operation is LFSOperation.Download)
        {
            return true;
        }

        if (!context.Request.Headers.TryGetCredentials(out var credentials))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("No usable Basic credentials on request to {Endpoint}", config.Endpoint);
            }

            return false;
        }

        var cacheDuration = TimeSpan.FromSeconds(appConfig.Options.GithubAuthCacheSeconds);

        if (cacheDuration <= TimeSpan.Zero)
        {
            return await QueryPermission(config, operation, credentials);
        }

        var cacheKey = BuildCacheKey(config.Endpoint, operation, credentials);

        if (cache.TryGetValue(cacheKey, out bool _))
        {
            return true;
        }

        var authorised = await QueryPermission(config, operation, credentials);

        if (authorised)
        {
            cache.Set(cacheKey, true, cacheDuration);
        }

        return authorised;
    }

    private async Task<bool> QueryPermission(EndpointConfig config, LFSOperation operation, BasicCredentials credentials)
    {
        try
        {
            var githubClient = new GitHubClient(new Connection(_headerValue, _githubUri))
            {
                Credentials = new Credentials(credentials.Username, credentials.Password),
            };

            var repository = await githubClient.Repository.Get(config.GithubSettings!.Organisation, config.GithubSettings!.Repository);

            return operation switch
            {
                LFSOperation.Download => repository.Permissions.Pull,
                LFSOperation.Upload => repository.Permissions.Push,
                _ => false,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub authentication failed");
            return false;
        }
    }

    private static string BuildCacheKey(string endpoint, LFSOperation operation, BasicCredentials credentials)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.Password}"));
        return $"gh:{endpoint}:{operation}:{Convert.ToHexStringLower(digest)}";
    }
}
