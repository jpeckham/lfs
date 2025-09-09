using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;

namespace SPTarkov.LFS.Authenticators;

public interface IAuthenticator
{
    /// <summary>
    /// Which authentication type this handles
    /// </summary>
    EAuthentication AuthenticationType { get; }

    /// <summary>
    /// The method to authenticate
    /// </summary>
    /// <returns>Returns <see cref="true"/> if authenticated successfully, returns <see cref="false"/> if not</returns>
    Task<bool> AuthenticateAsync(Config config, HttpContext context, LFSOperation operation);
}
