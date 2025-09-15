using System.Net;

namespace SPTarkov.LFS.Extensions;

public static class HttpContextExtensions
{
    public static string GetClientIpAddress(this HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;

        if (remoteIp is null || remoteIp.Equals(IPAddress.IPv6Loopback))
        {
            // First, try to get IP from X-Forwarded-For header (for when behind proxy)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // X-Forwarded-For can contain multiple IPs, and we only want the first one (client IP)
                var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (ips.Length > 0 && IPAddress.TryParse(ips[0], out _))
                {
                    return ips[0];
                }
            }

            // Try X-Real-IP (Available when behind nginx)
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
            {
                return realIp;
            }
        }

        return remoteIp?.ToString() ?? "Unknown";
    }
}
