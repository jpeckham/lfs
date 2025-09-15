using System.Text;

namespace SPTushonka.LFS.Extensions;

public static class HeaderDictionaryExtensions
{
    public const string AuthorizationHeader = "Authorization";
    public const string BasicPrefix = "Basic";

    public static (string Username, string Password) GetCredentials(this IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(AuthorizationHeader, out var authValues))
        {
            throw new InvalidOperationException("No Authorization header found.");
        }

        if (authValues.Count != 1)
        {
            throw new InvalidOperationException("More than one Authorization header found.");
        }

        var auth = authValues[0].AsSpan();

        if (!auth.StartsWith(BasicPrefix.AsSpan(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Authorization header is not Basic.");
        }

        var encodedCredentials = auth.Slice(BasicPrefix.Length).Trim();

        var decoded = Convert.FromBase64String(encodedCredentials.ToString());

        var decodedString = Encoding.UTF8.GetString(decoded);

        var colonIndex = decodedString.IndexOf(':');

        if (colonIndex == -1)
        {
            throw new InvalidOperationException("Invalid Basic authentication format.");
        }

        return (decodedString[..colonIndex], decodedString[(colonIndex + 1)..]);
    }
}
