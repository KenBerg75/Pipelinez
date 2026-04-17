using System.Text.Json;
using Ardalis.GuardClauses;
using Microsoft.Data.SqlClient;

namespace Pipelinez.SqlServer.Configuration;

/// <summary>
/// Provides shared SQL Server connection and serialization settings used by SQL Server transport components.
/// </summary>
public abstract class SqlServerOptions
{
    /// <summary>
    /// Gets or sets the connection string used by the transport.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Gets or sets an optional callback that can customize the <see cref="SqlConnectionStringBuilder" /> before connections are opened.
    /// </summary>
    public Action<SqlConnectionStringBuilder>? ConfigureConnectionString { get; init; }

    /// <summary>
    /// Gets or sets the default command timeout in seconds for SQL Server commands executed by the transport.
    /// </summary>
    public int? CommandTimeoutSeconds { get; init; }

    /// <summary>
    /// Gets or sets the JSON serializer options used by generated JSON mappings.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; init; } = new(JsonSerializerDefaults.Web);

    internal string BuildConnectionString(string optionsName)
    {
        ValidateCore(optionsName);

        var builder = new SqlConnectionStringBuilder(ConnectionString);
        ConfigureConnectionString?.Invoke(builder);
        return builder.ConnectionString;
    }

    internal void ValidateCore(string optionsName)
    {
        Guard.Against.NullOrWhiteSpace(optionsName, nameof(optionsName));
        Guard.Against.Null(SerializerOptions, nameof(SerializerOptions));

        if (CommandTimeoutSeconds is <= 0)
        {
            throw new InvalidOperationException($"{optionsName} command timeout must be greater than zero when provided.");
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException($"{optionsName} requires a ConnectionString.");
        }
    }
}
