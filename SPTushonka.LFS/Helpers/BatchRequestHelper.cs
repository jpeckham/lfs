using SPTarkov.DI.Annotations;
using SPTushonka.LFS.Models;
using SPTushonka.LFS.Models.LFS;
using SPTushonka.LFS.StorageAdapters;

namespace SPTushonka.LFS.Helpers;

[Injectable]
public class BatchRequestHelper(LocalStorageAdapter localStorageAdapter)
{
    public LFSObjectResponse HandleBatchRequestDownload(EndpointConfig endpointConfig, HttpContext context, LFSObject lfsObject)
    {
        var objectResponse = new LFSObjectResponse { ObjectId = lfsObject.ObjectId, Size = lfsObject.Size };

        var objectExists = localStorageAdapter.ObjectExists(endpointConfig.Endpoint, lfsObject.ObjectId);

        if (objectExists)
        {
            var actualSize = localStorageAdapter.GetObjectSize(endpointConfig.Endpoint, lfsObject.ObjectId);

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
                        Href = $"{context.Request.Scheme}://{context.Request.Host}{endpointConfig.Endpoint}/objects/{lfsObject.ObjectId}",
                        ExpiresIn = 3600,
                        //Todo: Set header here for authentication, read it in the authenticationservice again so that we don't have to auth with github everytime
                    },
                };
            }
        }
        else
        {
            objectResponse.Error = new LFSError { Code = 404, Message = "Object not found" };
        }

        return objectResponse;
    }

    public LFSObjectResponse HandleBatchRequestUpload(EndpointConfig endpointConfig, HttpContext context, LFSObject lfsObject)
    {
        var objectResponse = new LFSObjectResponse { ObjectId = lfsObject.ObjectId, Size = lfsObject.Size };

        var objectExists = localStorageAdapter.ObjectExists(endpointConfig.Endpoint, lfsObject.ObjectId);

        if (!objectExists)
        {
            objectResponse.Actions = new Dictionary<string, LFSAction>
            {
                ["upload"] = new LFSAction
                {
                    Href = $"{context.Request.Scheme}://{context.Request.Host}{endpointConfig.Endpoint}/objects/{lfsObject.ObjectId}",
                    ExpiresIn = 3600,
                    //Todo: Set header here for authentication, read it in the authenticationservice again so that we don't have to auth with github everytime
                },
            };
        }

        return objectResponse;
    }
}
