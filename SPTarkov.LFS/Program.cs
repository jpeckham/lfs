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

        List<string> registeredRoutes = [];

        foreach (var config in configs)
        {
            var mapGroup = app.MapGroup(config.Endpoint);
            RegisterLfsEndpoints(mapGroup, config);
            registeredRoutes.Add(config.Endpoint);
            logger.LogInformation("Added endpoint: {Endpoint}", config.Endpoint);
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
                await lfsService.HandleDownloadAsync(objectId, config, context)
        );

        // Upload object
        group.MapPut(
            "/objects/{objectId}",
            async ([FromServices] LfsService lfsService, string objectId, HttpRequest request, HttpContext context) =>
                await lfsService.HandleUploadAsync(objectId, request, config, context)
        );
    }
}
