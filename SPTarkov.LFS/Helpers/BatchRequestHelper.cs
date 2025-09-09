using SPTarkov.DI.Annotations;
using SPTarkov.LFS.Models;
using SPTarkov.LFS.Models.LFS;
using SPTarkov.LFS.StorageAdapters;

namespace SPTarkov.LFS.Helpers;

[Injectable]
public class BatchRequestHelper(LocalStorageAdapter localStorageAdapter)
{
    public LFSObjectResponse HandleBatchRequestDownload(Config config, HttpContext context, LFSObject lfsObject)
    {
        var objectResponse = new LFSObjectResponse { ObjectId = lfsObject.ObjectId, Size = lfsObject.Size };

        var objectExists = localStorageAdapter.ObjectExists(config.Endpoint, lfsObject.ObjectId);

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

    public LFSObjectResponse HandleBatchRequestUpload(Config config, HttpContext context, LFSObject lfsObject)
    {
        var objectResponse = new LFSObjectResponse { ObjectId = lfsObject.ObjectId, Size = lfsObject.Size };

        var objectExists = localStorageAdapter.ObjectExists(config.Endpoint, lfsObject.ObjectId);

        if (!objectExists)
        {
            objectResponse.Actions = new Dictionary<string, LFSAction>
            {
                ["upload"] = new LFSAction
                {
                    Href = $"{context.Request.Scheme}://{context.Request.Host}{config.Endpoint}/objects/{lfsObject.ObjectId}",
                    ExpiresIn = 3600,
                    //Todo: Set header here for authentication, read it in the authenticationservice again so that we don't have to auth with github everytime
                },
            };
        }

        return objectResponse;
    }
}
