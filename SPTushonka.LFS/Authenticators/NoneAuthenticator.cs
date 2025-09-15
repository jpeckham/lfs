using SPTarkov.DI.Annotations;
using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;

namespace SPTushonka.LFS.Authenticators;

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
