namespace Pipelinez.SqlServer.Configuration;

/// <summary>
/// Configures SQL Server destination writes for normal pipeline records.
/// </summary>
public sealed class SqlServerDestinationOptions : SqlServerOptions
{
    /// <summary>
    /// Validates the destination options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public SqlServerDestinationOptions Validate()
    {
        ValidateCore(nameof(SqlServerDestinationOptions));
        return this;
    }
}
