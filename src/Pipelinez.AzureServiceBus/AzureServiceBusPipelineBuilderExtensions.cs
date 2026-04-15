using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.DeadLettering;
using Pipelinez.AzureServiceBus.Destination;
using Pipelinez.AzureServiceBus.Source;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Record;

namespace Pipelinez.AzureServiceBus;

/// <summary>
/// Provides Azure Service Bus transport extension methods for <see cref="PipelineBuilder{T}" />.
/// </summary>
public static class AzureServiceBusPipelineBuilderExtensions
{
    /// <summary>
    /// Configures the pipeline to consume records from an Azure Service Bus queue or topic subscription.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Azure Service Bus source options.</param>
    /// <param name="recordMapper">Maps a Service Bus received message into a pipeline record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAzureServiceBusSource<T>(
        this PipelineBuilder<T> builder,
        AzureServiceBusSourceOptions options,
        Func<ServiceBusReceivedMessage, T> recordMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(recordMapper);

        return builder.WithSource(new AzureServiceBusPipelineSource<T>(
            options.Validate(),
            recordMapper));
    }

    /// <summary>
    /// Configures the pipeline to publish records to an Azure Service Bus queue or topic.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Azure Service Bus destination options.</param>
    /// <param name="messageMapper">Maps a pipeline record to the Service Bus message to publish.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAzureServiceBusDestination<T>(
        this PipelineBuilder<T> builder,
        AzureServiceBusDestinationOptions options,
        Func<T, ServiceBusMessage> messageMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(messageMapper);

        return builder.WithDestination(new AzureServiceBusPipelineDestination<T>(
            options.Validate(),
            messageMapper));
    }

    /// <summary>
    /// Configures the pipeline to write Pipelinez dead-letter records to an Azure Service Bus queue or topic.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Azure Service Bus dead-letter destination options.</param>
    /// <param name="messageMapper">Maps the dead-letter envelope into the Service Bus message to publish.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAzureServiceBusDeadLetterDestination<T>(
        this PipelineBuilder<T> builder,
        AzureServiceBusDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, ServiceBusMessage> messageMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(messageMapper);

        return builder.WithDeadLetterDestination(new AzureServiceBusDeadLetterDestination<T>(
            options.Validate(),
            messageMapper));
    }
}
