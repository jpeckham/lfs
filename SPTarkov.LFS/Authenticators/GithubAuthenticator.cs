using Octokit;
using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Extensions;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;

namespace SPTarkov.LFS.Authenticators;

[Injectable]
public class GithubAuthenticator(ILogger<GithubAuthenticator> logger) : IAuthenticator
{
    private static readonly ProductHeaderValue _headerValue = new("SPTarkov.LFS", Program.AppVersion);
    private static readonly Uri _githubUri = new("https://api.github.com/");

    public EAuthentication AuthenticationType
    {
        get { return EAuthentication.Github; }
    }

    public async Task<bool> AuthenticateAsync(Config config, HttpContext context, LFSOperation operation)
    {
        if (config.Visibility is EVisibility.Public && operation is LFSOperation.Download)
        {
            return true;
        }

        try
        {
            var (username, password) = context.Request.Headers.GetCredentials();

            var githubClient = new GitHubClient(new Connection(_headerValue, _githubUri))
            {
                Credentials = new Credentials(username, password),
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
}
