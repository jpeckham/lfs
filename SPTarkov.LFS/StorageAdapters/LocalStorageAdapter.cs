using System.Security.Cryptography;
using SPTarkov.DI.Annotations;

namespace SPTarkov.LFS.StorageAdapters;

[Injectable]
//Todo: Extend this further with an interface at some point, perhaps allowing S3 buckets and such to be a thing if we need them
public class LocalStorageAdapter
{
    private const string BaseStoragePath = "./data/";

    public LocalStorageAdapter()
    {
        Directory.CreateDirectory(BaseStoragePath);
    }

    public bool ObjectExists(string endpoint, string objectId)
    {
        var filePath = GetFilePath(endpoint, objectId);
        return File.Exists(filePath);
    }

    public FileStream GetObject(string endpoint, string objectId)
    {
        var filePath = GetFilePath(endpoint, objectId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"LFS object {objectId} not found");
        }

        return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 16384, useAsync: true);
    }

    public async Task StoreObject(string endpoint, string objectId, Stream content)
    {
        var filePath = GetFilePath(endpoint, objectId);
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        using var sha256 = SHA256.Create();
        await using var fileStream = File.Create(filePath);
        await using var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write);

        await content.CopyToAsync(cryptoStream);
        await cryptoStream.FlushFinalBlockAsync();

        if (sha256.Hash is null)
        {
            File.Delete(filePath);
            throw new InvalidOperationException("SHA-256 hash is null!");
        }

        var hashString = Convert.ToHexStringLower(sha256.Hash);

        if (!hashString.Equals(objectId, StringComparison.InvariantCultureIgnoreCase))
        {
            File.Delete(filePath);
            throw new InvalidOperationException($"SHA-256 hash mismatch. Expected {objectId}, got {hashString}");
        }
    }

    public long GetObjectSize(string endpoint, string objectId)
    {
        var filePath = GetFilePath(endpoint, objectId);

        if (!File.Exists(filePath))
        {
            return -1;
        }

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    private string GetFilePath(string endpoint, string objectId)
    {
        if (objectId.Length < 4)
        {
            throw new ArgumentException("Invalid objectId", nameof(objectId));
        }

        var endpointDir = SanitizeEndpointName(endpoint);
        var subDir1 = objectId.Substring(0, 2);
        var subDir2 = objectId.Substring(2, 2);
        return Path.Combine(BaseStoragePath, endpointDir, subDir1, subDir2, objectId);
    }

    private static string SanitizeEndpointName(string endpoint)
    {
        return endpoint.TrimStart('/').Replace('/', '_').Replace('\\', '_');
    }
}
