namespace Pipelinez.SqlServer.Configuration;

/// <summary>
/// Configures SQL Server writes for dead-letter records.
/// </summary>
public sealed class SqlServerDeadLetterOptions : SqlServerOptions
{
    /// <summary>
    /// Validates the dead-letter options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public SqlServerDeadLetterOptions Validate()
    {
        ValidateCore(nameof(SqlServerDeadLetterOptions));
        return this;
    }
}
