using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;

namespace SPTarkov.LFS.Authenticators;

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
