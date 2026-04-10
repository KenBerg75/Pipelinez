namespace Pipelinez.PostgreSql.Internal;

internal sealed record PostgreSqlColumnMap<TModel>(
    string ColumnName,
    Func<TModel, object?> ValueFactory,
    PostgreSqlColumnValueKind ValueKind);
