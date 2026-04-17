namespace Pipelinez.AmazonS3;

internal static class AmazonS3ObjectKey
{
    public static string Build(string? prefix, string key)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return key;
        }

        return $"{NormalizePrefix(prefix)}{TrimLeadingSlash(key)}";
    }

    public static string BuildTargetKey(string? sourcePrefix, string key, string targetPrefix)
    {
        var relativeKey = key;
        if (!string.IsNullOrEmpty(sourcePrefix) &&
            relativeKey.StartsWith(sourcePrefix, StringComparison.Ordinal))
        {
            relativeKey = relativeKey[sourcePrefix.Length..];
        }

        return Build(targetPrefix, TrimLeadingSlash(relativeKey));
    }

    public static string NormalizePrefix(string prefix)
    {
        var trimmed = TrimLeadingSlash(prefix.Trim());
        return trimmed.EndsWith("/", StringComparison.Ordinal)
            ? trimmed
            : $"{trimmed}/";
    }

    private static string TrimLeadingSlash(string value)
    {
        return value.TrimStart('/');
    }
}
