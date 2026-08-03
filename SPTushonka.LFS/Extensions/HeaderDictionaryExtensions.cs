using System.Text;

namespace SPTushonka.LFS.Extensions;

public readonly record struct BasicCredentials(string Username, string Password);

public static class HeaderDictionaryExtensions
{
    public const string AuthorizationHeader = "Authorization";
    public const string BasicPrefix = "Basic";

    public static bool TryGetCredentials(this IHeaderDictionary headers, out BasicCredentials credentials)
    {
        credentials = default;

        if (!headers.TryGetValue(AuthorizationHeader, out var authValues) || authValues.Count != 1)
        {
            return false;
        }

        var auth = authValues[0].AsSpan().Trim();

        // The scheme must be followed by a separator, otherwise "Basicxyz" would be accepted.
        if (auth.Length <= BasicPrefix.Length || !auth.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase) || auth[BasicPrefix.Length] != ' ')
        {
            return false;
        }

        var encoded = auth[BasicPrefix.Length..].Trim();
        var decoded = new byte[encoded.Length / 4 * 3];

        if (!Convert.TryFromBase64Chars(encoded, decoded, out var decodedLength))
        {
            return false;
        }

        var decodedString = Encoding.UTF8.GetString(decoded.AsSpan(0, decodedLength));
        var colonIndex = decodedString.IndexOf(':');

        if (colonIndex == -1)
        {
            return false;
        }

        credentials = new BasicCredentials(decodedString[..colonIndex], decodedString[(colonIndex + 1)..]);

        return true;
    }
}
