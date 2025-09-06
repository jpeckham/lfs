using System;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SPTarkov.DI;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;
using SPTarkov.LFS.Services;

namespace SPTarkov.LFS;

public class Program
{
    public static async Task Main(string[] args)
    {
        var configs = JsonSerializer.Deserialize<IReadOnlyList<Config>>(await File.ReadAllTextAsync("config.jsonc")) ?? [];

        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = null;
        });

        builder.Services.AddSingleton(configs);

        var diHandler = new DependencyInjectionHandler(builder.Services);
        diHandler.AddInjectableTypesFromTypeAssembly(typeof(Program));
        diHandler.InjectAll();

        var app = builder.Build();

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

        await app.RunAsync();
    }

    private static void RegisterLfsEndpoints(RouteGroupBuilder group, Config config)
    {
        // LFS Batch API
        group.MapPost(
            "/objects/batch",
            async ([FromServices] LfsService lfsService, [FromBody] LFSBatchRequest request, HttpContext context) =>
                await lfsService.HandleBatchRequest(request, config, context)
        );

        // Download object
        group.MapGet(
            "/objects/{objectId}",
            async ([FromServices] LfsService lfsService, string objectId, HttpContext context) =>
                await lfsService.HandleDownload(objectId, config, context)
        );

        // Upload object
        group.MapPut(
            "/objects/{objectId}",
            async ([FromServices] LfsService lfsService, string objectId, HttpRequest request, HttpContext context) =>
                await lfsService.HandleUpload(objectId, request, config, context)
        );
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
