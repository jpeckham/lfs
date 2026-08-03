using System.Text.RegularExpressions;

namespace SPTushonka.LFS.Helpers;

public static partial class LfsObjectId
{
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Pattern { get; }

    public static bool IsValid(string objectId)
    {
        return Pattern.IsMatch(objectId);
    }
}
