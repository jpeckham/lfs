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
    LocalStorageAdapter localStorageAdapter,
    Config config
)
{
    public async Task<IResult> HandleBatchRequest(LFSBatchRequest request, EndpointConfig endpointConfig, HttpContext context)
    {
        var authenticated = await authenticatorService.Authenticate(endpointConfig, context, request.Operation);

        if (!authenticated)
        {
            return Results.Unauthorized();
        }

        if (config.Options.AdditionalLogging)
        {
            logger.LogInformation(
                "[{EndpointName}] LFS batch request: {Operation} with {Count} objects for endpoint {Endpoint}",
                endpointConfig.Name,
                request.Operation,
                request.Objects.Length,
                endpointConfig.Endpoint
            );
        }

        List<LFSObjectResponse> objectResponses = [];

        foreach (var lfsObject in request.Objects)
        {
            switch (request.Operation)
            {
                case LFSOperation.Download:
                    objectResponses.Add(batchRequestHelper.HandleBatchRequestDownload(endpointConfig, context, lfsObject));
                    break;
                case LFSOperation.Upload:
                    objectResponses.Add(batchRequestHelper.HandleBatchRequestUpload(endpointConfig, context, lfsObject));
                    break;
            }
        }

        var response = new LFSBatchResponse { Objects = objectResponses };

        return Results.Ok(response);
    }

    public async Task<IResult> HandleDownload(string objectId, EndpointConfig endpointConfig, HttpContext context)
    {
        try
        {
            var authenticated = await authenticatorService.Authenticate(endpointConfig, context, LFSOperation.Download);

            if (!authenticated)
            {
                return Results.Unauthorized();
            }

            if (!localStorageAdapter.ObjectExists(endpointConfig.Endpoint, objectId))
            {
                return Results.NotFound();
            }

            return Results.Stream(
                localStorageAdapter.GetObject(endpointConfig.Endpoint, objectId),
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

    public async Task<IResult> HandleUpload(string objectId, HttpRequest request, EndpointConfig endPointConfig, HttpContext context)
    {
        try
        {
            var authenticated = await authenticatorService.Authenticate(endPointConfig, context, LFSOperation.Upload);

            if (!authenticated)
            {
                return Results.Unauthorized();
            }

            if (localStorageAdapter.ObjectExists(endPointConfig.Endpoint, objectId))
            {
                return Results.Ok();
            }

            var contentLength = request.ContentLength;

            if (!contentLength.HasValue)
            {
                return Results.BadRequest("Content-Length header is required");
            }

            await localStorageAdapter.StoreObject(endPointConfig.Endpoint, objectId, request.Body);

            if (config.Options.AdditionalLogging)
            {
                logger.LogInformation(
                    "[{EndpointName}] Successfully stored object {ObjectId} ({Size} bytes)",
                    endPointConfig.Name,
                    objectId,
                    contentLength.Value
                );
            }

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
