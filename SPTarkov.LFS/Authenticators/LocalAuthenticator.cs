using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Models;

namespace SPTarkov.LFS.Authenticators;

[Injectable]
public class LocalAuthenticator : IAuthenticator
{
    public EAuthentication AuthenticationType
    {
        get { return EAuthentication.Local; }
    }

    public Task<bool> AuthenticateAsync(Config config, HttpContext context, string operation)
    {
        //Todo: Implement
        throw new NotImplementedException();
    }
}
