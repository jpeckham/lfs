using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using SPTarkov.DI;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;
using SPTarkov.LFS.Services;

namespace SPTarkov.LFS;

public class Program
{
    public static string AppVersion
    {
        get
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? throw new NullReferenceException("Could not get AppVersion!");
        }
    }

    public static async Task Main(string[] args)
    {
        var configs = JsonSerializer.Deserialize<IReadOnlyList<Config>>(await File.ReadAllTextAsync("config.jsonc")) ?? [];

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://*:5420");

        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("default", httpContext =>
            {
                var ipAddress = GetClientIpAddress(httpContext);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ipAddress,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10000,
                        Window = TimeSpan.FromHours(24),
                        QueueLimit = 100,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });
            options.RejectionStatusCode = 429;
            options.OnRejected = (context, _) =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var ipAddress = GetClientIpAddress(context.HttpContext);
                logger.LogWarning("Rate limit exceeded for IP: {IpAddress} on endpoint: {Path}", ipAddress, context.HttpContext.Request.Path);
                return ValueTask.CompletedTask;
            };
        });

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = null;
        });

        builder.Services.AddSingleton(configs);

        var diHandler = new DependencyInjectionHandler(builder.Services);
        diHandler.AddInjectableTypesFromTypeAssembly(typeof(Program));
        diHandler.InjectAll();

        var app = builder.Build();

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = null,
        };
        forwardedHeadersOptions.KnownNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);

        // Middleware to log IP detection for debugging
        // To be removed once we confirm IP detection works as expected
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var clientIp = GetClientIpAddress(context);
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();

            logger.LogInformation(
                "Request from - Resolved IP: {ClientIp}, X-Forwarded-For: {ForwardedFor}, X-Real-IP: {RealIp}, RemoteIpAddress: {RemoteIp}, Path: {Path}",
                clientIp, forwardedFor ?? "none", realIp ?? "none", remoteIp ?? "none", context.Request.Path);

            await next();
        });

        app.UseRateLimiter();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        if (configs.Count == 0)
        {
            logger.LogError("No configs were entered!");
            return;
        }

        foreach (var repositoryConfig in configs)
        {
            if (repositoryConfig.Authentication is EAuthentication.Github)
            {
                var repoSettings = GetGithubSettingsFromString(repositoryConfig.Url);

                if (repoSettings is null)
                {
                    logger.LogError(
                        "Skipping endpoint '{Endpoint}' due to invalid GitHub repository URL: {RepositoryUrl}",
                        repositoryConfig.Endpoint,
                        repositoryConfig.Url
                    );
                }

                repositoryConfig.GithubSettings = repoSettings;
            }

            var mapGroup = app.MapGroup(repositoryConfig.Endpoint);
            RegisterLfsEndpoints(mapGroup, repositoryConfig);
            logger.LogInformation("Added endpoint: {Endpoint}", repositoryConfig.Endpoint);
        }

        app.MapGet("/", () => Results.Content($"<h1>SPT.LFS v{AppVersion}</h1>", "text/html")).RequireRateLimiting("default");

        await app.RunAsync();
    }

    private static void RegisterLfsEndpoints(RouteGroupBuilder group, Config config)
    {
        // LFS Batch API
        group
            .MapPost(
                "/objects/batch",
                async ([FromServices] LfsService lfsService, [FromBody] LFSBatchRequest request, HttpContext context) =>
                    await lfsService.HandleBatchRequest(request, config, context)
            )
            .RequireRateLimiting("default");

        // Download object
        group
            .MapGet(
                "/objects/{objectId}",
                async ([FromServices] LfsService lfsService, string objectId, HttpContext context) =>
                    await lfsService.HandleDownload(objectId, config, context)
            )
            .RequireRateLimiting("default");

        // Upload object (Not rate limited)
        group.MapPut(
            "/objects/{objectId}",
            async ([FromServices] LfsService lfsService, string objectId, HttpRequest request, HttpContext context) =>
                await lfsService.HandleUpload(objectId, request, config, context)
        );
    }

    private static string GetClientIpAddress(HttpContext context)
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

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            return "";
        }

        return remoteIp.Equals(IPAddress.IPv6Loopback) ? "127.0.0.1" : remoteIp.ToString();
    }

    private static GithubSettings? GetGithubSettingsFromString(string Url)
    {
        var uri = new Uri(Url);

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');

        if (segments.Length < 2)
        {
            return null;
        }

        var organisation = segments[0];
        var repository = segments[1];

        return new GithubSettings { Organisation = organisation, Repository = repository };
    }
}
