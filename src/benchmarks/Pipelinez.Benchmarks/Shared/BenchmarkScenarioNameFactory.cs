namespace Pipelinez.Benchmarks;

internal static class BenchmarkScenarioNameFactory
{
    public static string CreateKafkaTopicName(string benchmarkName, string suffix)
    {
        return CreateKebabName("pipelinez-bench", benchmarkName, suffix, 180);
    }

    public static string CreateKafkaConsumerGroupName(string benchmarkName, string suffix)
    {
        return CreateKebabName("pipelinez-bench-group", benchmarkName, suffix, 180);
    }

    public static string CreateQueueName(string benchmarkName, string suffix)
    {
        return CreateKebabName("pipelinez-bench", benchmarkName, suffix, 180);
    }

    public static string CreateExchangeName(string benchmarkName, string suffix)
    {
        return CreateKebabName("pipelinez-bench", benchmarkName, suffix, 180);
    }

    public static string CreateBucketName(string benchmarkName, string suffix)
    {
        return CreateKebabName("pipelinez-bench", benchmarkName, suffix, 63);
    }

    public static string CreateObjectPrefix(string benchmarkName, string suffix)
    {
        return $"{CreateKebabName("bench", benchmarkName, suffix, 48)}/";
    }

    public static string CreateSchemaName(string benchmarkName, string suffix)
    {
        return CreateSnakeName("schema", benchmarkName, suffix, 100);
    }

    public static string CreateTableName(string benchmarkName, string suffix)
    {
        return CreateSnakeName("table", benchmarkName, suffix, 110);
    }

    private static string CreateKebabName(string prefix, string benchmarkName, string suffix, int maxLength)
    {
        var guid = Guid.NewGuid().ToString("N");
        var value = $"{NormalizeKebab(prefix)}-{NormalizeKebab(benchmarkName)}-{NormalizeKebab(suffix)}-{guid}"
            .Trim('-');

        if (value.Length <= maxLength)
        {
            return value;
        }

        var prefixWithoutGuid = $"{NormalizeKebab(prefix)}-{NormalizeKebab(benchmarkName)}-{NormalizeKebab(suffix)}"
            .Trim('-');
        var availableLength = Math.Max(1, maxLength - guid.Length - 1);
        var truncatedPrefix = prefixWithoutGuid[..Math.Min(prefixWithoutGuid.Length, availableLength)].Trim('-');
        return $"{truncatedPrefix}-{guid}".Trim('-');
    }

    private static string CreateSnakeName(string prefix, string benchmarkName, string suffix, int maxLength)
    {
        var guid = Guid.NewGuid().ToString("N");
        var value = $"{NormalizeSnake(prefix)}_{NormalizeSnake(benchmarkName)}_{NormalizeSnake(suffix)}_{guid}"
            .Trim('_');

        if (value.Length <= maxLength)
        {
            return value;
        }

        var prefixWithoutGuid = $"{NormalizeSnake(prefix)}_{NormalizeSnake(benchmarkName)}_{NormalizeSnake(suffix)}"
            .Trim('_');
        var availableLength = Math.Max(1, maxLength - guid.Length - 1);
        var truncatedPrefix = prefixWithoutGuid[..Math.Min(prefixWithoutGuid.Length, availableLength)].Trim('_');
        return $"{truncatedPrefix}_{guid}".Trim('_');
    }

    private static string NormalizeKebab(string value)
    {
        return Normalize(value, '-');
    }

    private static string NormalizeSnake(string value)
    {
        return Normalize(value, '_');
    }

    private static string Normalize(string value, char separator)
    {
        var buffer = new char[value.Length];
        var index = 0;
        var previousWasSeparator = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[index++] = char.ToLowerInvariant(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            buffer[index++] = separator;
            previousWasSeparator = true;
        }

        return new string(buffer, 0, index).Trim(separator);
    }
}
