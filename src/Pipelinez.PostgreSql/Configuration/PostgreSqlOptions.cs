using System.Text.Json;
using Ardalis.GuardClauses;
using Npgsql;

namespace Pipelinez.PostgreSql.Configuration;

/// <summary>
/// Provides shared PostgreSQL connection and serialization settings used by PostgreSQL transport components.
/// </summary>
public abstract class PostgreSqlOptions
{
    /// <summary>
    /// Gets or sets the connection string used when the transport builds its own <see cref="NpgsqlDataSource" />.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Gets or sets an optional callback that can customize the <see cref="NpgsqlConnectionStringBuilder" /> before the data source is built.
    /// </summary>
    public Action<NpgsqlConnectionStringBuilder>? ConfigureConnectionString { get; init; }

    /// <summary>
    /// Gets or sets an optional callback that can customize the <see cref="NpgsqlDataSourceBuilder" /> before the data source is built.
    /// </summary>
    public Action<NpgsqlDataSourceBuilder>? ConfigureDataSource { get; init; }

    /// <summary>
    /// Gets or sets an externally owned <see cref="NpgsqlDataSource" /> to use for command execution.
    /// </summary>
    /// <remarks>
    /// When provided, <see cref="ConnectionString" />, <see cref="ConfigureConnectionString" />, and
    /// <see cref="ConfigureDataSource" /> must not also be supplied.
    /// </remarks>
    public NpgsqlDataSource? DataSource { get; init; }

    /// <summary>
    /// Gets or sets the default command timeout in seconds for PostgreSQL commands executed by the transport.
    /// </summary>
    public int? CommandTimeoutSeconds { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether parameter logging should be enabled when the transport builds its own data source.
    /// </summary>
    public bool EnableSensitiveLogging { get; init; }

    /// <summary>
    /// Gets or sets the JSON serializer options used by generated JSON mappings.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; init; } = new(JsonSerializerDefaults.Web);

    internal void ValidateCore(string optionsName)
    {
        Guard.Against.NullOrWhiteSpace(optionsName, nameof(optionsName));
        Guard.Against.Null(SerializerOptions, nameof(SerializerOptions));

        if (CommandTimeoutSeconds is <= 0)
        {
            throw new InvalidOperationException($"{optionsName} command timeout must be greater than zero when provided.");
        }

        if (DataSource is not null)
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString) ||
                ConfigureConnectionString is not null ||
                ConfigureDataSource is not null)
            {
                throw new InvalidOperationException(
                    $"{optionsName} cannot combine an external NpgsqlDataSource with connection-string or data-source builder configuration.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                $"{optionsName} requires either a ConnectionString or an external NpgsqlDataSource.");
        }
    }
}
