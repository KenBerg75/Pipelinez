namespace Pipelinez.AmazonS3.Configuration;

internal static class AmazonS3Validation
{
    public static void ValidateObjectKey(string key, string valueName)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"{valueName} is required.");
        }

        if (key.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException($"{valueName} cannot contain control characters.");
        }
    }

    public static void ValidateOptionalKeyPrefix(string? prefix, string valueName)
    {
        if (prefix is not null && prefix.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException($"{valueName} cannot contain control characters.");
        }
    }

    public static void ValidateDictionary(IReadOnlyDictionary<string, string> values, string valueName)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value.Key))
            {
                throw new InvalidOperationException($"{valueName} keys cannot be empty.");
            }

            ArgumentNullException.ThrowIfNull(value.Value);
        }
    }
}
