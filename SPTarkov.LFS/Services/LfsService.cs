using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Helpers;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;
using SPTarkov.LFS.StorageAdapters;

namespace SPTarkov.LFS.Services;

[Injectable]
public class LfsService(
    ILogger<LfsService> logger,
    AuthenticatorService authenticatorService,
    BatchRequestHelper batchRequestHelper,
    LocalStorageAdapter localStorageAdapter
)
{
    public async Task<IResult> HandleBatchRequest(LFSBatchRequest request, Config config, HttpContext context)
    {
        var authenticated = await authenticatorService.Authenticate(config, context, request.Operation);

        if (!authenticated)
        {
            return Results.Unauthorized();
        }

        logger.LogInformation(
            "LFS batch request: {Operation} with {Count} objects for endpoint {Endpoint}",
            request.Operation,
            request.Objects.Length,
            config.Endpoint
        );

        List<LFSObjectResponse> objectResponses = [];

        foreach (var lfsObject in request.Objects)
        {
            switch (request.Operation)
            {
                case LFSOperation.Download:
                    objectResponses.Add(batchRequestHelper.HandleBatchRequestDownload(config, context, lfsObject));
                    break;
                case LFSOperation.Upload:
                    objectResponses.Add(batchRequestHelper.HandleBatchRequestUpload(config, context, lfsObject));
                    break;
            }
        }

        var response = new LFSBatchResponse { Objects = objectResponses };

        return Results.Ok(response);
    }

    public async Task<IResult> HandleDownload(string objectId, Config config, HttpContext context)
    {
        try
        {
            var authenticated = await authenticatorService.Authenticate(config, context, LFSOperation.Download);

            if (!authenticated)
            {
                return Results.Unauthorized();
            }

            if (!localStorageAdapter.ObjectExists(config.Endpoint, objectId))
            {
                return Results.NotFound();
            }

            return Results.Stream(
                localStorageAdapter.GetObject(config.Endpoint, objectId),
                "application/octet-stream",
                enableRangeProcessing: true
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading object {ObjectId}", objectId);
            return Results.InternalServerError();
        }
    }

    public async Task<IResult> HandleUpload(string objectId, HttpRequest request, Config config, HttpContext context)
    {
        try
        {
            var authenticated = await authenticatorService.Authenticate(config, context, LFSOperation.Upload);

            if (!authenticated)
            {
                return Results.Unauthorized();
            }

            if (localStorageAdapter.ObjectExists(config.Endpoint, objectId))
            {
                return Results.Ok();
            }

            var contentLength = request.ContentLength;

            if (!contentLength.HasValue)
            {
                return Results.BadRequest("Content-Length header is required");
            }

            await localStorageAdapter.StoreObject(config.Endpoint, objectId, request.Body);

            logger.LogInformation("Successfully stored object {ObjectId} ({Size} bytes)", objectId, contentLength.Value);
            return Results.Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("hash mismatch"))
        {
            logger.LogWarning("Hash mismatch for object {ObjectId}: {Message}", objectId, ex.Message);
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading object {ObjectId}", objectId);
            return Results.Problem("Internal server error", statusCode: 500);
        }
    }
}
