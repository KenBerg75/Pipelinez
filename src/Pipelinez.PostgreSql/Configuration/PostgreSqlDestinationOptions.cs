namespace Pipelinez.PostgreSql.Configuration;

/// <summary>
/// Configures PostgreSQL destination writes for normal pipeline records.
/// </summary>
public sealed class PostgreSqlDestinationOptions : PostgreSqlOptions
{
    /// <summary>
    /// Validates the destination options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public PostgreSqlDestinationOptions Validate()
    {
        ValidateCore(nameof(PostgreSqlDestinationOptions));
        return this;
    }
}
