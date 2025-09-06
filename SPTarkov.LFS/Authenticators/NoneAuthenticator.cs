using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Models;

namespace SPTarkov.LFS.Authenticators;

[Injectable]
public class NoneAuthenticator : IAuthenticator
{
    public EAuthentication AuthenticationType
    {
        get { return EAuthentication.None; }
    }

    public Task<bool> AuthenticateAsync(Config config, HttpContext context, string operation)
    {
        return Task.FromResult(true);
    }
}
