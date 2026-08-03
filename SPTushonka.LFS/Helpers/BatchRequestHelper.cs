using SPTarkov.DI.Annotations;
using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;
using SPTushonka.LFS.StorageAdapters;

namespace SPTushonka.LFS.Helpers;

[Injectable]
public class BatchRequestHelper(LocalStorageAdapter localStorageAdapter, Config config)
{
    public LFSObjectResponse HandleBatchRequestDownload(EndpointConfig endpointConfig, HttpContext context, LFSObject lfsObject)
    {
        var objectResponse = new LFSObjectResponse { ObjectId = lfsObject.ObjectId, Size = lfsObject.Size };

        if (!LfsObjectId.IsValid(lfsObject.ObjectId))
        {
            objectResponse.Error = new LFSError { Code = 422, Message = "Invalid object id" };
            return objectResponse;
        }

        if (!localStorageAdapter.ObjectExists(endpointConfig.Endpoint, lfsObject.ObjectId))
        {
            objectResponse.Error = new LFSError { Code = 404, Message = "Object not found" };
            return objectResponse;
        }

        var actualSize = localStorageAdapter.GetObjectSize(endpointConfig.Endpoint, lfsObject.ObjectId);

        if (actualSize != lfsObject.Size)
        {
            objectResponse.Error = new LFSError
            {
                Code = 422,
                Message = $"Object size mismatch. Expected {lfsObject.Size}, found {actualSize}",
            };
            return objectResponse;
        }

        objectResponse.Actions = BuildAction("download", endpointConfig, context, lfsObject);

        return objectResponse;
    }

    public LFSObjectResponse HandleBatchRequestUpload(EndpointConfig endpointConfig, HttpContext context, LFSObject lfsObject)
    {
        var objectResponse = new LFSObjectResponse { ObjectId = lfsObject.ObjectId, Size = lfsObject.Size };

        if (!LfsObjectId.IsValid(lfsObject.ObjectId))
        {
            objectResponse.Error = new LFSError { Code = 422, Message = "Invalid object id" };
            return objectResponse;
        }

        var maxUploadSize = config.Options.MaxUploadSizeBytes;

        if (maxUploadSize > 0 && lfsObject.Size > maxUploadSize)
        {
            objectResponse.Error = new LFSError
            {
                Code = 413,
                Message = $"Object exceeds the maximum upload size of {maxUploadSize} bytes",
            };
            return objectResponse;
        }

        // No actions means the object is already present and the client should skip it.
        if (!localStorageAdapter.ObjectExists(endpointConfig.Endpoint, lfsObject.ObjectId))
        {
            objectResponse.Actions = BuildAction("upload", endpointConfig, context, lfsObject);
        }

        return objectResponse;
    }

    private static Dictionary<string, LFSAction> BuildAction(
        string action,
        EndpointConfig endpointConfig,
        HttpContext context,
        LFSObject lfsObject
    )
    {
        return new Dictionary<string, LFSAction>
        {
            [action] = new LFSAction
            {
                Href = $"{context.Request.Scheme}://{context.Request.Host}{endpointConfig.Endpoint}/objects/{lfsObject.ObjectId}",
                ExpiresIn = 3600,
                //Todo: Set header here for authentication, read it in the authenticationservice again so that we don't have to auth with github everytime
            },
        };
    }
}
