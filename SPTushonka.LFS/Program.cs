using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SPTarkov.DI;
using SPTushonka.LFS.Extensions;
using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;
using SPTushonka.LFS.Services;

namespace SPTushonka.LFS;

public class Program
{
    private const string ConfigPathEnvironmentVariable = "SPT_LFS_CONFIG";
    private const string DefaultConfigPath = "config.jsonc";
    private const string RateLimitPolicy = "default";

    private static readonly JsonSerializerOptions _configSerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string AppVersion
    {
        get
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? throw new NullReferenceException("Could not get AppVersion!");
        }
    }

    public static async Task<int> Main(string[] args)
    {
        var config = await LoadConfig();

        if (config is null)
        {
            return 1;
        }

        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseUrls("http://*:5420");
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = config.Options.MaxUploadSizeBytes > 0 ? config.Options.MaxUploadSizeBytes : null;
        });

        builder.Services.Configure<ForwardedHeadersOptions>(options => ConfigureForwardedHeaders(options, config));
        builder.Services.AddRateLimiter(options => ConfigureRateLimiter(options, config));
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(config);

        var diHandler = new DependencyInjectionHandler(builder.Services);
        diHandler.AddInjectableTypesFromTypeAssembly(typeof(Program));
        diHandler.InjectAll();

        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        app.UseForwardedHeaders();

        app.Use(LogRequestOrigin);
        app.UseRateLimiter();

        if (config.Options.TrustedProxies.Length == 0)
        {
            logger.LogWarning(
                "Options.TrustedProxies is empty, so X-Forwarded-For is accepted from any source. "
                    + "Behind a reverse proxy, list its address to stop clients spoofing their IP and bypassing rate limits."
            );
        }

        var registered = RegisterEndpoints(app, config, logger);

        if (registered == 0)
        {
            logger.LogError("No usable endpoints were configured, shutting down.");
            return 1;
        }

        app.MapGet("/", () => Results.Content($"<h1>SPTushonka.LFS v{AppVersion}</h1>", "text/html")).RequireRateLimiting(RateLimitPolicy);
        app.MapGet("/health", () => Results.Ok(new { status = "ok", version = AppVersion }));

        await app.RunAsync();

        return 0;
    }

    private static async Task<Config?> LoadConfig()
    {
        var path = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable) ?? DefaultConfigPath;

        if (!File.Exists(path))
        {
            Console.Error.WriteLine(
                $"Config file '{Path.GetFullPath(path)}' does not exist. "
                    + $"Set {ConfigPathEnvironmentVariable} to point at it, or place {DefaultConfigPath} next to the executable."
            );
            return null;
        }

        try
        {
            var config =
                JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(path), _configSerializerOptions)
                ?? throw new InvalidOperationException("the file is empty");

            return config;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Config file '{Path.GetFullPath(path)}' could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Registers every valid endpoint and returns how many were mapped. Invalid entries are logged
    /// and skipped rather than taking the whole application down.
    /// </summary>
    private static int RegisterEndpoints(WebApplication app, Config config, ILogger logger)
    {
        if (config.Endpoints.Count == 0)
        {
            logger.LogError("No endpoints were entered!");
            return 0;
        }

        var seenEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var registered = 0;

        foreach (var repositoryConfig in config.Endpoints)
        {
            if (!IsEndpointValid(repositoryConfig, seenEndpoints, logger))
            {
                continue;
            }

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
                    continue;
                }

                repositoryConfig.GithubSettings = repoSettings;
            }

            RegisterLfsEndpoints(app.MapGroup(repositoryConfig.Endpoint), repositoryConfig);
            seenEndpoints.Add(repositoryConfig.Endpoint);
            registered++;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Added endpoint: {Endpoint}", repositoryConfig.Endpoint);
            }
        }

        return registered;
    }

    private static bool IsEndpointValid(EndpointConfig repositoryConfig, HashSet<string> seenEndpoints, ILogger logger)
    {
        var endpoint = repositoryConfig.Endpoint;

        if (string.IsNullOrWhiteSpace(endpoint) || !endpoint.StartsWith('/') || (endpoint.Length > 1 && endpoint.EndsWith('/')))
        {
            logger.LogError(
                "Skipping endpoint '{Endpoint}' for '{Name}': it must start with '/' and must not end with one.",
                endpoint,
                repositoryConfig.Name
            );
            return false;
        }

        if (seenEndpoints.Contains(endpoint))
        {
            logger.LogError("Skipping duplicate endpoint '{Endpoint}' for '{Name}'.", endpoint, repositoryConfig.Name);
            return false;
        }

        // Registering it would map routes that reject every request at runtime.
        if (repositoryConfig.Authentication is EAuthentication.Local)
        {
            logger.LogError("Skipping endpoint '{Endpoint}': Local authentication is not implemented yet.", endpoint);
            return false;
        }

        return true;
    }

    private static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, Config config)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = null;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        // With no entries the defaults stay cleared, which trusts forwarded headers from anywhere.
        foreach (var proxy in config.Options.TrustedProxies)
        {
            if (System.Net.IPNetwork.TryParse(proxy, out var network))
            {
                options.KnownIPNetworks.Add(network);
                continue;
            }

            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
                continue;
            }

            Console.Error.WriteLine($"Ignoring unparseable Options.TrustedProxies entry '{proxy}', expected an IP address or CIDR range.");
        }
    }

    private static void ConfigureRateLimiter(RateLimiterOptions options, Config config)
    {
        var limits = config.Options.RateLimit;

        options.AddPolicy(
            RateLimitPolicy,
            httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit,
                        Window = TimeSpan.FromHours(limits.WindowHours),
                        QueueLimit = limits.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    }
                )
        );

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = (context, _) =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                "Rate limit exceeded for IP: {IpAddress} on endpoint: {Path}",
                context.HttpContext.Connection.RemoteIpAddress,
                context.HttpContext.Request.Path
            );
            return ValueTask.CompletedTask;
        };
    }

    /// <summary>
    /// Records where a request appeared to come from, for diagnosing reverse proxy setups.
    /// Enable with a Debug level on the SPTushonka.LFS category.
    /// </summary>
    private static async Task LogRequestOrigin(HttpContext context, RequestDelegate next)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        if (!logger.IsEnabled(LogLevel.Debug))
        {
            await next(context);
            return;
        }

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();

        var ipInfo = $"Client: {context.Connection.RemoteIpAddress}";

        if (!string.IsNullOrEmpty(forwardedFor))
        {
            ipInfo += $", X-Forwarded-For: {forwardedFor}";
        }

        if (!string.IsNullOrEmpty(realIp))
        {
            ipInfo += $", X-Real-IP: {realIp}";
        }

        logger.LogDebug("Request from {IpInfo}, Path: {Path}", ipInfo, context.Request.Path);

        await next(context);
    }

    private static void RegisterLfsEndpoints(RouteGroupBuilder group, EndpointConfig config)
    {
        // LFS Batch API
        group
            .MapPost(
                "/objects/batch",
                async ([FromServices] LfsService lfsService, [FromBody] LFSBatchRequest request, HttpContext context) =>
                    await lfsService.HandleBatchRequest(request, config, context)
            )
            .RequireRateLimiting(RateLimitPolicy);

        // Download object
        group
            .MapGet(
                "/objects/{objectId}",
                async ([FromServices] LfsService lfsService, string objectId, HttpContext context) =>
                    await lfsService.HandleDownload(objectId, config, context)
            )
            .RequireRateLimiting(RateLimitPolicy);

        // Upload object (Not rate limited)
        group.MapPut(
            "/objects/{objectId}",
            async ([FromServices] LfsService lfsService, string objectId, HttpRequest request, HttpContext context) =>
                await lfsService.HandleUpload(objectId, request, config, context)
        );
    }

    private static GithubSettings? GetGithubSettingsFromString(string Url)
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');

        if (segments.Length < 2 || string.IsNullOrWhiteSpace(segments[0]) || string.IsNullOrWhiteSpace(segments[1]))
        {
            return null;
        }

        return new GithubSettings { Organisation = segments[0], Repository = segments[1] };
    }
}
