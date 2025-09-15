using SPTarkov.DI.Annotations;
using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;

namespace SPTushonka.LFS.Authenticators;

[Injectable]
public class LocalAuthenticator : IAuthenticator
{
    public EAuthentication AuthenticationType
    {
        get { return EAuthentication.Local; }
    }

    public Task<bool> AuthenticateAsync(EndpointConfig endpointConfig, HttpContext context, LFSOperation operation)
    {
        //Todo: Implement
        throw new NotImplementedException();
    }
}
