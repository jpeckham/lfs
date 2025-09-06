using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Authenticators;
using SPTarkov.LFS.Models;

namespace SPTarkov.LFS.Services;

[Injectable]
public class AuthenticatorService(IEnumerable<IAuthenticator> authenticators)
{
    public async Task<bool> Authenticate(Config config, HttpContext context, string operation)
    {
        var authenticator =
            authenticators.FirstOrDefault(a => a.AuthenticationType == config.Authentication)
            ?? throw new InvalidOperationException($"No authenticator found for type: {config.Authentication}");

        return await authenticator.AuthenticateAsync(config, context, operation);
    }
}
