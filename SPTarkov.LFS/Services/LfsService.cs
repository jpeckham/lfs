using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;
using SPTarkov.LFS.StorageAdapters;

namespace SPTarkov.LFS.Services;

[Injectable]
public class LfsService(ILogger<LfsService> logger, AuthenticatorService authenticatorService, LocalStorageAdapter localStorageAdapter)
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

        var objectResponses = new List<LFSObjectResponse>();

        foreach (var lfsObject in request.Objects)
        {
            var objectResponse = new LFSObjectResponse { ObjectId = lfsObject.ObjectId, Size = lfsObject.Size };

            var objectExists = await localStorageAdapter.ExistsAsync(config.Endpoint, lfsObject.ObjectId);

            if (request.Operation == "download")
            {
                if (objectExists)
                {
                    var actualSize = localStorageAdapter.GetObjectSize(config.Endpoint, lfsObject.ObjectId);

                    if (actualSize != lfsObject.Size)
                    {
                        objectResponse.Error = new LFSError
                        {
                            Code = 422,
                            Message = $"Object size mismatch. Expected {lfsObject.Size}, found {actualSize}",
                        };
                    }
                    else
                    {
                        objectResponse.Actions = new Dictionary<string, LFSAction>
                        {
                            ["download"] = new LFSAction
                            {
                                Href = $"{context.Request.Scheme}://{context.Request.Host}{config.Endpoint}/objects/{lfsObject.ObjectId}",
                                ExpiresIn = 3600,
                            },
                        };
                    }
                }
                else
                {
                    objectResponse.Error = new LFSError { Code = 404, Message = "Object not found" };
                }
            }
            else if (request.Operation == "upload")
            {
                if (!objectExists)
                {
                    objectResponse.Actions = new Dictionary<string, LFSAction>
                    {
                        ["upload"] = new LFSAction
                        {
                            Href = $"{context.Request.Scheme}://{context.Request.Host}{config.Endpoint}/objects/{lfsObject.ObjectId}",
                            ExpiresIn = 3600,
                        },
                    };
                    objectResponse.Authenticated = false;
                }
            }

            objectResponses.Add(objectResponse);
        }

        var response = new LFSBatchResponse { Objects = objectResponses.ToArray() };

        return Results.Ok(response);
    }

    public async Task<IResult> HandleDownload(string objectId, Config config, HttpContext context)
    {
        try
        {
            var authenticated = await authenticatorService.Authenticate(config, context, "download");

            if (!authenticated)
            {
                return Results.Unauthorized();
            }

            if (!await localStorageAdapter.ExistsAsync(config.Endpoint, objectId))
            {
                return Results.NotFound();
            }

            var stream = await localStorageAdapter.GetObjectAsync(config.Endpoint, objectId);
            return Results.Stream(stream, "application/octet-stream", enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error downloading object {objectId}");
            return Results.Problem("Internal server error", statusCode: 500);
        }
    }

    public async Task<IResult> HandleUpload(string objectId, HttpRequest request, Config config, HttpContext context)
    {
        try
        {
            var authenticated = await authenticatorService.Authenticate(config, context, "upload");

            if (!authenticated)
            {
                return Results.Unauthorized();
            }

            if (await localStorageAdapter.ExistsAsync(config.Endpoint, objectId))
            {
                return Results.Ok();
            }

            var contentLength = request.ContentLength;

            if (!contentLength.HasValue)
            {
                return Results.BadRequest("Content-Length header is required");
            }

            await localStorageAdapter.StoreObjectAsync(config.Endpoint, objectId, request.Body);

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
