using Ardalis.GuardClauses;
using Pipelinez.PostgreSql.Internal;

namespace Pipelinez.PostgreSql.Mapping;

/// <summary>
/// Defines a strongly typed mapping from a model object to a PostgreSQL table.
/// </summary>
/// <typeparam name="TModel">The model type used to produce column values.</typeparam>
public sealed class PostgreSqlTableMap<TModel>
{
    private readonly IReadOnlyList<PostgreSqlColumnMap<TModel>> _columns;

    private PostgreSqlTableMap(
        string schemaName,
        string tableName,
        IReadOnlyList<PostgreSqlColumnMap<TModel>> columns)
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

    internal IReadOnlyList<PostgreSqlColumnMap<TModel>> Columns => _columns;

    /// <summary>
    /// Creates a new empty table map for the specified schema and table.
    /// </summary>
    /// <param name="schemaName">The schema name to target.</param>
    /// <param name="tableName">The table name to target.</param>
    /// <returns>A new table map instance.</returns>
    public static PostgreSqlTableMap<TModel> ForTable(string schemaName, string tableName)
    {
        return new PostgreSqlTableMap<TModel>(schemaName, tableName, Array.Empty<PostgreSqlColumnMap<TModel>>());
    }

    /// <summary>
    /// Adds a column mapping that writes a scalar or parameterized value directly.
    /// </summary>
    /// <typeparam name="TValue">The value type produced by the mapping.</typeparam>
    /// <param name="columnName">The destination column name.</param>
    /// <param name="valueFactory">The callback that produces the column value from the model.</param>
    /// <returns>A new table map with the added column mapping.</returns>
    public PostgreSqlTableMap<TModel> Map<TValue>(string columnName, Func<TModel, TValue> valueFactory)
    {
        Guard.Against.Null(valueFactory, nameof(valueFactory));
        return AddColumn(new PostgreSqlColumnMap<TModel>(
            Guard.Against.NullOrWhiteSpace(columnName, nameof(columnName)),
            model => valueFactory(model),
            PostgreSqlColumnValueKind.Default));
    }

    /// <summary>
    /// Adds a column mapping that serializes the produced value as JSON and writes it as <c>jsonb</c>.
    /// </summary>
    /// <typeparam name="TValue">The value type produced by the mapping.</typeparam>
    /// <param name="columnName">The destination column name.</param>
    /// <param name="valueFactory">The callback that produces the value to serialize.</param>
    /// <returns>A new table map with the added JSON column mapping.</returns>
    public PostgreSqlTableMap<TModel> MapJson<TValue>(string columnName, Func<TModel, TValue> valueFactory)
    {
        Guard.Against.Null(valueFactory, nameof(valueFactory));
        return AddColumn(new PostgreSqlColumnMap<TModel>(
            Guard.Against.NullOrWhiteSpace(columnName, nameof(columnName)),
            model => valueFactory(model),
            PostgreSqlColumnValueKind.Jsonb));
    }

    /// <summary>
    /// Validates the table map and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated table map.</returns>
    public PostgreSqlTableMap<TModel> Validate()
    {
        PostgreSqlIdentifier.QuoteQualified(SchemaName, TableName);

        if (_columns.Count == 0)
        {
            throw new InvalidOperationException("A PostgreSQL table map must define at least one column mapping.");
        }

        foreach (var column in _columns)
        {
            PostgreSqlIdentifier.Quote(column.ColumnName);
        }

        return this;
    }

    private PostgreSqlTableMap<TModel> AddColumn(PostgreSqlColumnMap<TModel> column)
    {
        var columns = _columns.ToList();
        columns.Add(column);
        return new PostgreSqlTableMap<TModel>(SchemaName, TableName, columns);
    }
}
