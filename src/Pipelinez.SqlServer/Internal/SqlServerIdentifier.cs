using Ardalis.GuardClauses;

namespace Pipelinez.SqlServer.Internal;

internal static class SqlServerIdentifier
{
    public static string Quote(string identifier)
    {
        var value = Guard.Against.NullOrWhiteSpace(identifier, nameof(identifier)).Trim();

        if (value.IndexOfAny(['\r', '\n', '\0', ';']) >= 0)
        {
            throw new InvalidOperationException($"Identifier '{identifier}' contains characters that are not supported in SQL Server identifier quoting.");
        }

        return "[" + value.Replace("]", "]]") + "]";
    }

    public static string QuoteQualified(string schemaName, string tableName)
    {
        return $"{Quote(schemaName)}.{Quote(tableName)}";
    }
}
