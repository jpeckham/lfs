using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;

namespace SPTarkov.LFS.Authenticators;

[Injectable]
public class NoneAuthenticator : IAuthenticator
{
    public EAuthentication AuthenticationType
    {
        get { return EAuthentication.None; }
    }

    public Task<bool> AuthenticateAsync(EndpointConfig endpontConfig, HttpContext context, LFSOperation operation)
    {
        return Task.FromResult(true);
    }
}
