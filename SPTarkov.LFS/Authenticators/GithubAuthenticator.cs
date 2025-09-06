using Octokit;
using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Extensions;
using SPTarkov.LFS.Models;

namespace SPTarkov.LFS.Authenticators;

[Injectable]
public class GithubAuthenticator(ILogger<GithubAuthenticator> logger) : IAuthenticator
{
    private static readonly ProductHeaderValue _headerValue = new("SPTarkov.LFS", "1.0.0");
    private static readonly Uri _githubUri = new("https://api.github.com/");

    public EAuthentication AuthenticationType
    {
        get { return EAuthentication.Github; }
    }

    public async Task<bool> AuthenticateAsync(Config config, HttpContext context, string operation)
    {
        if (config.Visibility is EVisibility.Public && operation is "download")
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

            // Todo: Handle better, should be verified at program start and if not possible to verify the config should be skipped.
            // Cache in the config model
            var uri = new Uri(config.Url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');

            var organisation = segments.Length > 0 ? segments[0] : null;
            var repo = segments.Length > 1 ? segments[1] : null;

            var repository = await githubClient.Repository.Get(organisation, repo);

            return operation switch
            {
                "download" => repository.Permissions.Pull,
                "upload" => repository.Permissions.Push,
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
