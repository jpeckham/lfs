using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;

namespace SPTushonka.LFS.Authenticators;

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
    Task<bool> AuthenticateAsync(EndpointConfig config, HttpContext context, LFSOperation operation);
}
