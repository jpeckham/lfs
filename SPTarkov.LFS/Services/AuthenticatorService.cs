using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Authenticators;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;

namespace SPTarkov.LFS.Services;

[Injectable]
public class AuthenticatorService(IEnumerable<IAuthenticator> authenticators)
{
    public async Task<bool> Authenticate(EndpointConfig endpointConfig, HttpContext context, LFSOperation operation)
    {
        var authenticator =
            authenticators.FirstOrDefault(a => a.AuthenticationType == endpointConfig.Authentication)
            ?? throw new InvalidOperationException($"No authenticator found for type: {endpointConfig.Authentication}");

        return await authenticator.AuthenticateAsync(endpointConfig, context, operation);
    }
}
