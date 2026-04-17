using Ardalis.GuardClauses;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Record;
using Pipelinez.SqlServer.Configuration;
using Pipelinez.SqlServer.DeadLettering;
using Pipelinez.SqlServer.Destination;
using Pipelinez.SqlServer.Internal;
using Pipelinez.SqlServer.Mapping;

namespace Pipelinez.SqlServer;

/// <summary>
/// Provides SQL Server transport extension methods for <see cref="PipelineBuilder{T}" />.
/// </summary>
public static class SqlServerPipelineBuilderExtensions
{
    /// <summary>
    /// Configures the pipeline to write successful records to SQL Server using a generated insert from a table map.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The SQL Server destination options.</param>
    /// <param name="tableMap">The table map that defines the destination schema, table, and columns.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithSqlServerDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        SqlServerDestinationOptions options,
        SqlServerTableMap<TRecord> tableMap)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tableMap);

        var validatedOptions = options.Validate();
        var validatedMap = tableMap.Validate();

        return builder.WithDestination(new SqlServerPipelineDestination<TRecord>(
            validatedOptions,
            record => SqlServerMappedCommandFactory.CreateCommand(
                validatedMap,
                Guard.Against.Null(record, nameof(record)),
                validatedOptions.SerializerOptions,
                validatedOptions.CommandTimeoutSeconds)));
    }

    /// <summary>
    /// Configures the pipeline to write successful records to SQL Server using consumer-provided SQL.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The SQL Server destination options.</param>
    /// <param name="commandFactory">The callback that creates a parameterized command definition for each record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithSqlServerDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        SqlServerDestinationOptions options,
        Func<TRecord, SqlServerCommandDefinition> commandFactory)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(commandFactory);

        return builder.WithDestination(new SqlServerPipelineDestination<TRecord>(
            options.Validate(),
            record => Guard.Against.Null(commandFactory(record), nameof(commandFactory)).Validate()));
    }

    /// <summary>
    /// Configures the pipeline to write dead-letter records to SQL Server using a generated insert from a table map.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type contained in the dead-letter envelope.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The SQL Server dead-letter destination options.</param>
    /// <param name="tableMap">The table map that defines the dead-letter schema, table, and columns.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithSqlServerDeadLetterDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        SqlServerDeadLetterOptions options,
        SqlServerTableMap<PipelineDeadLetterRecord<TRecord>> tableMap)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tableMap);

        var validatedOptions = options.Validate();
        var validatedMap = tableMap.Validate();

        return builder.WithDeadLetterDestination(new SqlServerDeadLetterDestination<TRecord>(
            validatedOptions,
            deadLetterRecord => SqlServerMappedCommandFactory.CreateCommand(
                validatedMap,
                Guard.Against.Null(deadLetterRecord, nameof(deadLetterRecord)),
                validatedOptions.SerializerOptions,
                validatedOptions.CommandTimeoutSeconds)));
    }

    /// <summary>
    /// Configures the pipeline to write dead-letter records to SQL Server using consumer-provided SQL.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type contained in the dead-letter envelope.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The SQL Server dead-letter destination options.</param>
    /// <param name="commandFactory">The callback that creates a parameterized command definition for each dead-letter record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithSqlServerDeadLetterDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        SqlServerDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<TRecord>, SqlServerCommandDefinition> commandFactory)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(commandFactory);

        return builder.WithDeadLetterDestination(new SqlServerDeadLetterDestination<TRecord>(
            options.Validate(),
            deadLetterRecord => Guard.Against.Null(commandFactory(deadLetterRecord), nameof(commandFactory)).Validate()));
    }
}
