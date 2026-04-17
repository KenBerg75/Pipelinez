using Amazon.S3.Model;

namespace Pipelinez.AmazonS3;

internal static class AmazonS3Tags
{
    public static List<Tag> Merge(
        IReadOnlyDictionary<string, string> first,
        IReadOnlyDictionary<string, string> second)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var tag in first)
        {
            tags[tag.Key] = tag.Value;
        }

        foreach (var tag in second)
        {
            tags[tag.Key] = tag.Value;
        }

        return tags
            .OrderBy(tag => tag.Key, StringComparer.Ordinal)
            .Select(tag => new Tag { Key = tag.Key, Value = tag.Value })
            .ToList();
    }

    public static IReadOnlyDictionary<string, string> ToDictionary(IEnumerable<Tag> tags)
    {
        return tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal);
    }
}
