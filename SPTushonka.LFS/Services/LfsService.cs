using SPTarkov.DI.Annotations;
using SPTushonka.LFS.Helpers;
using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;
using SPTushonka.LFS.StorageAdapters;

namespace SPTushonka.LFS.Services;

[Injectable]
public class LfsService(
    ILogger<LfsService> logger,
    AuthenticatorService authenticatorService,
    BatchRequestHelper batchRequestHelper,
    LocalStorageAdapter localStorageAdapter,
    Config config
)
{
    private const string LfsContentType = "application/vnd.git-lfs+json";

    public async Task<IResult> HandleBatchRequest(LFSBatchRequest request, EndpointConfig endpointConfig, HttpContext context)
    {
        try
        {
            if (request.Operation is not (LFSOperation.Download or LFSOperation.Upload))
            {
                return LfsError(422, "Unsupported operation, expected 'download' or 'upload'");
            }

            var authenticated = await authenticatorService.Authenticate(endpointConfig, context, request.Operation);

            if (!authenticated)
            {
                return Results.Unauthorized();
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "[{EndpointName}] LFS batch request: {Operation} with {Count} objects for endpoint {Endpoint}",
                    endpointConfig.Name,
                    request.Operation,
                    request.Objects.Length,
                    endpointConfig.Endpoint
                );
            }

            var objectResponses = request
                .Objects.Select(lfsObject =>
                    request.Operation is LFSOperation.Download
                        ? batchRequestHelper.HandleBatchRequestDownload(endpointConfig, context, lfsObject)
                        : batchRequestHelper.HandleBatchRequestUpload(endpointConfig, context, lfsObject)
                )
                .ToList();

            return Results.Json(new LFSBatchResponse { Objects = objectResponses }, contentType: LfsContentType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling batch request for endpoint {Endpoint}", endpointConfig.Endpoint);
            return LfsError(500, "Internal server error");
        }
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

            if (!LfsObjectId.IsValid(objectId))
            {
                return LfsError(422, "Invalid object id");
            }

            if (!localStorageAdapter.ObjectExists(endpointConfig.Endpoint, objectId))
            {
                return LfsError(404, "Object not found");
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
            return LfsError(500, "Internal server error");
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

            if (!LfsObjectId.IsValid(objectId))
            {
                return LfsError(422, "Invalid object id");
            }

            if (localStorageAdapter.ObjectExists(endPointConfig.Endpoint, objectId))
            {
                return Results.Ok();
            }

            var contentLength = request.ContentLength;

            if (!contentLength.HasValue)
            {
                return LfsError(400, "Content-Length header is required");
            }

            var maxUploadSize = config.Options.MaxUploadSizeBytes;

            if (maxUploadSize > 0 && contentLength.Value > maxUploadSize)
            {
                logger.LogWarning(
                    "[{EndpointName}] Rejected object {ObjectId}: {Size} bytes exceeds the {Limit} byte limit",
                    endPointConfig.Name,
                    objectId,
                    contentLength.Value,
                    maxUploadSize
                );
                return LfsError(413, $"Object exceeds the maximum upload size of {maxUploadSize} bytes");
            }

            await localStorageAdapter.StoreObject(endPointConfig.Endpoint, objectId, request.Body);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
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
            return LfsError(400, ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Upload of object {ObjectId} was aborted by the client", objectId);
            }

            return Results.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading object {ObjectId}", objectId);
            return LfsError(500, "Internal server error");
        }
    }

    private static IResult LfsError(int statusCode, string message)
    {
        return Results.Json(new LFSError { Code = statusCode, Message = message }, contentType: LfsContentType, statusCode: statusCode);
    }
}
