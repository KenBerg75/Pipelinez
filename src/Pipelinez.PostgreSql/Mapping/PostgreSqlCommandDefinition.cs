using Ardalis.GuardClauses;

namespace Pipelinez.PostgreSql.Mapping;

/// <summary>
/// Represents a parameterized PostgreSQL command that the transport can execute through Dapper.
/// </summary>
public sealed class PostgreSqlCommandDefinition
{
    /// <summary>
    /// Initializes a new PostgreSQL command definition.
    /// </summary>
    /// <param name="commandText">The SQL command text to execute.</param>
    /// <param name="parameters">The parameter object passed to Dapper.</param>
    /// <param name="commandTimeoutSeconds">An optional command timeout override in seconds.</param>
    public PostgreSqlCommandDefinition(string commandText, object? parameters, int? commandTimeoutSeconds = null)
    {
        CommandText = Guard.Against.NullOrWhiteSpace(commandText, nameof(commandText));
        Parameters = parameters;

        if (commandTimeoutSeconds is <= 0)
        {
            throw new InvalidOperationException("Command timeout must be greater than zero when provided.");
        }

        CommandTimeoutSeconds = commandTimeoutSeconds;
    }

    /// <summary>
    /// Gets the parameterized SQL command text to execute.
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// Gets the parameter object passed to Dapper for command execution.
    /// </summary>
    public object? Parameters { get; }

    /// <summary>
    /// Gets the optional command timeout override in seconds.
    /// </summary>
    public int? CommandTimeoutSeconds { get; }

    /// <summary>
    /// Validates the command definition and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated command definition.</returns>
    public PostgreSqlCommandDefinition Validate()
    {
        if (string.IsNullOrWhiteSpace(CommandText))
        {
            throw new InvalidOperationException("Command text is required.");
        }

        if (CommandTimeoutSeconds is <= 0)
        {
            throw new InvalidOperationException("Command timeout must be greater than zero when provided.");
        }

        return this;
    }
}
