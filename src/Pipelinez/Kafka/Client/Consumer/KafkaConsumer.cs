using Ardalis.GuardClauses;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Logging;
using Pipelinez.Kafka.Configuration;

namespace Pipelinez.Kafka.Client.Consumer;

internal class KafkaConsumer<TMessageKey, TMessageValue> : IKafkaConsumer<TMessageKey, TMessageValue> where TMessageKey : class where TMessageValue : class
{
    private readonly KafkaSourceOptions _options;
    private readonly string _pipelineName;
    private IConsumer<TMessageKey, TMessageValue> _consumer;
    private readonly ILogger<KafkaConsumer<TMessageKey, TMessageValue>> _logger;

    internal KafkaConsumer(string pipelineName, KafkaSourceOptions kafkaSourceOptions)
    {
        _logger = LoggingManager.Instance.CreateLogger<KafkaConsumer<TMessageKey, TMessageValue>>();
        _pipelineName = pipelineName;
        _options = kafkaSourceOptions;
    }
    
    #region Build

    /// <summary>
    /// Builds the consumer so it is ready for use.
    /// </summary>
    internal KafkaConsumer<TMessageKey, TMessageValue> Build()
    {
        InitializeConsumer();
        return this;
    }

    private void InitializeConsumer()
    {
        var builder = new ConsumerBuilder<TMessageKey, TMessageValue>(_options.ToConsumerConfig(_pipelineName));
        SetConsumerDeserializers(builder, _options.Schema);
        SetupLogHandlers(builder);
        _consumer = builder.Build();
    }

    private ConsumerBuilder<TMessageKey, TMessageValue> SetupLogHandlers(ConsumerBuilder<TMessageKey, TMessageValue> builder)
    {
        builder.SetPartitionsRevokedHandler((c, partitions) =>
            {
                _logger.LogInformation("consumer {MemberId} had partitions {Partitions} revoked", c.MemberId, string.Join(",", partitions));
            })
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation("consumer {MemberId} had partitions {Partitions} assigned", c.MemberId, string.Join(",", partitions));
                return partitions.Select(tp => new TopicPartitionOffset(tp, _options.StartOffsetFromBeginning ? Offset.Beginning : Offset.Stored));
            })
            .SetOffsetsCommittedHandler((c, committedOffsets) =>
            {
                if (committedOffsets.Error.IsError)
                {
                    if (committedOffsets.Error.IsFatal)
                    {
                        // Fatal and we break everything
                        throw new Exception(committedOffsets.Error.Reason);
                    }

                    _logger.LogError(new Exception(committedOffsets.Error.Reason),
                        "Error committing offsets. {@CommittedOffsets}", committedOffsets);
                }
                else
                {
                    _logger.LogInformation("Offsets committed [{CommittedOffsets}]", string.Join(",", committedOffsets.Offsets.Select(o => o.Offset.Value).ToArray()));
                }
            })
            .SetErrorHandler((_, e) => _logger.LogError("Consume Error: {Reason}", e.Reason)); // TODO: Revisit

        return builder;
    }

    /// <summary>
    /// Configures the Serializers for a Consumer based on the configuration
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="options"></param>
    /// <typeparam name="TKey">Type for the Key</typeparam>
    /// <typeparam name="TValue">Type for the Value</typeparam>
    /// <returns></returns>
    private static ConsumerBuilder<TKey, TValue> SetConsumerDeserializers<TKey, TValue>(
        ConsumerBuilder<TKey, TValue> builder,
        KafkaSchemaRegistryOptions? options) where TKey : class where TValue : class
    {
        var keyDeserializationType = options.GetKeyDeserializationType();
        var valueDeserializationType = options.GetValueDeserializationType();

        // Key Deserialization
        // For DEFAULT, we don't apply serializers
        if (keyDeserializationType == KafkaDeserializationType.AVRO)
        {
            builder.SetKeyDeserializer(new AvroDeserializer<TKey>(BuildSchemaRegistryClient(options))
                .AsSyncOverAsync());
        }

        if (keyDeserializationType == KafkaDeserializationType.JSON)
        {
            builder.SetKeyDeserializer(new JsonDeserializer<TKey>().AsSyncOverAsync());
        }


        // Value Deserialization
        // For DEFAULT, we don't apply deserializers
        if (valueDeserializationType == KafkaDeserializationType.AVRO)
        {
            builder.SetValueDeserializer(new AvroDeserializer<TValue>(BuildSchemaRegistryClient(options))
                .AsSyncOverAsync());
        }

        if (valueDeserializationType == KafkaDeserializationType.JSON)
        {
            builder.SetValueDeserializer(new JsonDeserializer<TValue>().AsSyncOverAsync());
        }

        return builder;
    }
    
    #region Schema Registry Client
    
    
    private static ISchemaRegistryClient BuildSchemaRegistryClient(KafkaSchemaRegistryOptions? config)
    {
        Guard.Against.Null(config, nameof(config), "KafkaSchemaRegistryOptions is required in KafkaSourceOptions to build a Schema Registry Client");
        
        return new CachedSchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = config.Server,
            BasicAuthUserInfo = $"{config.User}:{config.Password}"
        });
    }
    
    #endregion
    
    #endregion

    #region IKafkaConsumer

    public string Name
    {
        get { return _consumer.Name; }
    }

    public List<string> Subscription 
    {
        get { return _consumer.Subscription; }
    }
    public string MemberId 
    {
        get { return _consumer.MemberId; }
    }

    public void Subscribe(string topicName)
    {
        _consumer.Subscribe(topicName);
    }

    public ConsumeResult<TMessageKey, TMessageValue> Consume(TimeSpan timeout)
    {
        return _consumer.Consume(timeout);
    }

    public void StoreOffset(TopicPartitionOffset topicPartitionOffset)
    {
        _consumer.StoreOffset(topicPartitionOffset);
    }

    #endregion

}