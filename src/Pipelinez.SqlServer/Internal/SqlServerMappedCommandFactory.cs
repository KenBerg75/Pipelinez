using System.Text.Json;
using Ardalis.GuardClauses;
using Dapper;
using Pipelinez.SqlServer.Mapping;

namespace Pipelinez.SqlServer.Internal;

internal static class SqlServerMappedCommandFactory
{
    public static SqlServerCommandDefinition CreateCommand<TModel>(
        SqlServerTableMap<TModel> tableMap,
        TModel model,
        JsonSerializerOptions serializerOptions,
        int? defaultCommandTimeoutSeconds)
    {
        var validatedMap = Guard.Against.Null(tableMap, nameof(tableMap)).Validate();
        Guard.Against.Null(serializerOptions, nameof(serializerOptions));

        var parameters = new DynamicParameters();
        var quotedColumns = new List<string>(validatedMap.Columns.Count);
        var parameterExpressions = new List<string>(validatedMap.Columns.Count);

        for (var index = 0; index < validatedMap.Columns.Count; index++)
        {
            var column = validatedMap.Columns[index];
            var parameterName = $"p{index}";
            quotedColumns.Add(SqlServerIdentifier.Quote(column.ColumnName));

            var value = column.ValueFactory(model);
            switch (column.ValueKind)
            {
                case SqlServerColumnValueKind.Json:
                    parameters.Add(parameterName, value is null ? null : JsonSerializer.Serialize(value, serializerOptions));
                    parameterExpressions.Add($"@{parameterName}");
                    break;
                default:
                    parameters.Add(parameterName, value);
                    parameterExpressions.Add($"@{parameterName}");
                    break;
            }
        }

        var commandText =
            $"INSERT INTO {SqlServerIdentifier.QuoteQualified(validatedMap.SchemaName, validatedMap.TableName)} " +
            $"({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", parameterExpressions)});";

        return new SqlServerCommandDefinition(commandText, parameters, defaultCommandTimeoutSeconds);
    }
}
