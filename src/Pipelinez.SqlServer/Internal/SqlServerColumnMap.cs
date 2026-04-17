namespace Pipelinez.SqlServer.Internal;

internal sealed record SqlServerColumnMap<TModel>(
    string ColumnName,
    Func<TModel, object?> ValueFactory,
    SqlServerColumnValueKind ValueKind);
