using Ardalis.GuardClauses;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Record;
using Pipelinez.PostgreSql.Configuration;
using Pipelinez.PostgreSql.DeadLettering;
using Pipelinez.PostgreSql.Destination;
using Pipelinez.PostgreSql.Internal;
using Pipelinez.PostgreSql.Mapping;

namespace Pipelinez.PostgreSql;

/// <summary>
/// Provides PostgreSQL transport extension methods for <see cref="PipelineBuilder{T}" />.
/// </summary>
public static class PostgreSqlPipelineBuilderExtensions
{
    /// <summary>
    /// Configures the pipeline to write successful records to PostgreSQL using a generated insert from a table map.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The PostgreSQL destination options.</param>
    /// <param name="tableMap">The table map that defines the destination schema, table, and columns.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithPostgreSqlDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        PostgreSqlDestinationOptions options,
        PostgreSqlTableMap<TRecord> tableMap)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tableMap);

        var validatedOptions = options.Validate();
        var validatedMap = tableMap.Validate();

        return builder.WithDestination(new PostgreSqlPipelineDestination<TRecord>(
            validatedOptions,
            record => PostgreSqlMappedCommandFactory.CreateCommand(
                validatedMap,
                Guard.Against.Null(record, nameof(record)),
                validatedOptions.SerializerOptions,
                validatedOptions.CommandTimeoutSeconds)));
    }

    /// <summary>
    /// Configures the pipeline to write successful records to PostgreSQL using consumer-provided SQL.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The PostgreSQL destination options.</param>
    /// <param name="commandFactory">The callback that creates a parameterized command definition for each record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithPostgreSqlDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        PostgreSqlDestinationOptions options,
        Func<TRecord, PostgreSqlCommandDefinition> commandFactory)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(commandFactory);

        return builder.WithDestination(new PostgreSqlPipelineDestination<TRecord>(
            options.Validate(),
            record => Guard.Against.Null(commandFactory(record), nameof(commandFactory)).Validate()));
    }

    /// <summary>
    /// Configures the pipeline to write dead-letter records to PostgreSQL using a generated insert from a table map.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type contained in the dead-letter envelope.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The PostgreSQL dead-letter destination options.</param>
    /// <param name="tableMap">The table map that defines the dead-letter schema, table, and columns.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithPostgreSqlDeadLetterDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        PostgreSqlDeadLetterOptions options,
        PostgreSqlTableMap<PipelineDeadLetterRecord<TRecord>> tableMap)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tableMap);

        var validatedOptions = options.Validate();
        var validatedMap = tableMap.Validate();

        return builder.WithDeadLetterDestination(new PostgreSqlDeadLetterDestination<TRecord>(
            validatedOptions,
            deadLetterRecord => PostgreSqlMappedCommandFactory.CreateCommand(
                validatedMap,
                Guard.Against.Null(deadLetterRecord, nameof(deadLetterRecord)),
                validatedOptions.SerializerOptions,
                validatedOptions.CommandTimeoutSeconds)));
    }

    /// <summary>
    /// Configures the pipeline to write dead-letter records to PostgreSQL using consumer-provided SQL.
    /// </summary>
    /// <typeparam name="TRecord">The pipeline record type contained in the dead-letter envelope.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The PostgreSQL dead-letter destination options.</param>
    /// <param name="commandFactory">The callback that creates a parameterized command definition for each dead-letter record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<TRecord> WithPostgreSqlDeadLetterDestination<TRecord>(
        this PipelineBuilder<TRecord> builder,
        PostgreSqlDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<TRecord>, PostgreSqlCommandDefinition> commandFactory)
        where TRecord : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(commandFactory);

        return builder.WithDeadLetterDestination(new PostgreSqlDeadLetterDestination<TRecord>(
            options.Validate(),
            deadLetterRecord => Guard.Against.Null(commandFactory(deadLetterRecord), nameof(commandFactory)).Validate()));
    }
}
