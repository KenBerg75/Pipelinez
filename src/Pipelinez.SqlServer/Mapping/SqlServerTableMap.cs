using Ardalis.GuardClauses;
using Pipelinez.SqlServer.Internal;

namespace Pipelinez.SqlServer.Mapping;

/// <summary>
/// Defines a strongly typed mapping from a model object to a SQL Server table.
/// </summary>
/// <typeparam name="TModel">The model type used to produce column values.</typeparam>
public sealed class SqlServerTableMap<TModel>
{
    private readonly IReadOnlyList<SqlServerColumnMap<TModel>> _columns;

    private SqlServerTableMap(
        string schemaName,
        string tableName,
        IReadOnlyList<SqlServerColumnMap<TModel>> columns)
    {
        SchemaName = Guard.Against.NullOrWhiteSpace(schemaName, nameof(schemaName));
        TableName = Guard.Against.NullOrWhiteSpace(tableName, nameof(tableName));
        _columns = Guard.Against.Null(columns, nameof(columns));
    }

    /// <summary>
    /// Gets the schema name of the destination table.
    /// </summary>
    public string SchemaName { get; }

    /// <summary>
    /// Gets the table name of the destination table.
    /// </summary>
    public string TableName { get; }

    internal IReadOnlyList<SqlServerColumnMap<TModel>> Columns => _columns;

    /// <summary>
    /// Creates a new empty table map for the specified schema and table.
    /// </summary>
    /// <param name="schemaName">The schema name to target.</param>
    /// <param name="tableName">The table name to target.</param>
    /// <returns>A new table map instance.</returns>
    public static SqlServerTableMap<TModel> ForTable(string schemaName, string tableName)
    {
        return new SqlServerTableMap<TModel>(schemaName, tableName, Array.Empty<SqlServerColumnMap<TModel>>());
    }

    /// <summary>
    /// Adds a column mapping that writes a scalar or parameterized value directly.
    /// </summary>
    /// <typeparam name="TValue">The value type produced by the mapping.</typeparam>
    /// <param name="columnName">The destination column name.</param>
    /// <param name="valueFactory">The callback that produces the column value from the model.</param>
    /// <returns>A new table map with the added column mapping.</returns>
    public SqlServerTableMap<TModel> Map<TValue>(string columnName, Func<TModel, TValue> valueFactory)
    {
        Guard.Against.Null(valueFactory, nameof(valueFactory));
        return AddColumn(new SqlServerColumnMap<TModel>(
            Guard.Against.NullOrWhiteSpace(columnName, nameof(columnName)),
            model => valueFactory(model),
            SqlServerColumnValueKind.Default));
    }

    /// <summary>
    /// Adds a column mapping that serializes the produced value as JSON text.
    /// </summary>
    /// <typeparam name="TValue">The value type produced by the mapping.</typeparam>
    /// <param name="columnName">The destination column name.</param>
    /// <param name="valueFactory">The callback that produces the value to serialize.</param>
    /// <returns>A new table map with the added JSON column mapping.</returns>
    public SqlServerTableMap<TModel> MapJson<TValue>(string columnName, Func<TModel, TValue> valueFactory)
    {
        Guard.Against.Null(valueFactory, nameof(valueFactory));
        return AddColumn(new SqlServerColumnMap<TModel>(
            Guard.Against.NullOrWhiteSpace(columnName, nameof(columnName)),
            model => valueFactory(model),
            SqlServerColumnValueKind.Json));
    }

    /// <summary>
    /// Validates the table map and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated table map.</returns>
    public SqlServerTableMap<TModel> Validate()
    {
        SqlServerIdentifier.QuoteQualified(SchemaName, TableName);

        if (_columns.Count == 0)
        {
            throw new InvalidOperationException("A SQL Server table map must define at least one column mapping.");
        }

        foreach (var column in _columns)
        {
            SqlServerIdentifier.Quote(column.ColumnName);
        }

        return this;
    }

    private SqlServerTableMap<TModel> AddColumn(SqlServerColumnMap<TModel> column)
    {
        var columns = _columns.ToList();
        columns.Add(column);
        return new SqlServerTableMap<TModel>(SchemaName, TableName, columns);
    }
}
