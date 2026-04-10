namespace Pipelinez.PostgreSql.Configuration;

/// <summary>
/// Configures PostgreSQL writes for dead-letter records.
/// </summary>
public sealed class PostgreSqlDeadLetterOptions : PostgreSqlOptions
{
    /// <summary>
    /// Validates the dead-letter options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public PostgreSqlDeadLetterOptions Validate()
    {
        ValidateCore(nameof(PostgreSqlDeadLetterOptions));
        return this;
    }
}
