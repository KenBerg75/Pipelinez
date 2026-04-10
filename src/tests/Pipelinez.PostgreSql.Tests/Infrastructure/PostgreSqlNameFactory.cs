namespace Pipelinez.PostgreSql.Tests.Infrastructure;

public static class PostgreSqlNameFactory
{
    public static string CreateSchemaName(string scenarioName)
    {
        return CreateName("schema", scenarioName);
    }

    public static string CreateTableName(string scenarioName)
    {
        return CreateName("table", scenarioName);
    }

    private static string CreateName(string prefix, string scenarioName)
    {
        var normalized = new string(
            scenarioName
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray())
            .Trim('_');

        return $"{prefix}_{normalized}_{Guid.NewGuid():N}";
    }
}
