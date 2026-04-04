using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Logging;
using Pipelinez.Kafka.Configuration;

namespace Pipelinez.Kafka.Client.Producer;

internal class KafkaProducer<TRecordKey, TRecordValue> : IKafkaProducer<TRecordKey, TRecordValue> where TRecordValue : class where TRecordKey : class
{
    private readonly KafkaDestinationOptions _options;
    private readonly string _pipelineName;
    private IProducer<TRecordKey, TRecordValue>? _producer;
    private readonly ILogger<KafkaProducer<TRecordKey, TRecordValue>> _logger;
    
    public KafkaProducer(string pipelineName, KafkaDestinationOptions options)
    {
        _logger = LoggingManager.Instance.CreateLogger<KafkaProducer<TRecordKey, TRecordValue>>();
        _pipelineName = pipelineName;
        _options = options;
    }

    #region Build
    
    /// <summary>
    /// Must be called to start the producer.
    /// This is generally done by the factory creating the producer
    /// </summary>
    /// <returns></returns>
    public KafkaProducer<TRecordKey, TRecordValue> Build()
    {
        InitializeProducer();
        return this;
    }

    private void InitializeProducer()
    {
        var finalConfig = _options.ToProducerConfig(_pipelineName);
        var builder = new ProducerBuilder<TRecordKey, TRecordValue>(finalConfig);
        builder = SetProducerSerializers(builder, _options.Schema);
        builder = SetProducerHandlers(builder);
        _producer = builder.Build();

    }
    
    /// <summary>
    /// Configures the Serializers for a Producer based on the configuration
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    private static ProducerBuilder<TRecordKey, TRecordValue> SetProducerSerializers(ProducerBuilder<TRecordKey, TRecordValue> builder,
        KafkaSchemaRegistryOptions? options)
    {
        if (options is null)
        {
            return builder;
        }

        var keySerializationType = options.GetKeySerializationType();
        var valueSerializationType = options.GetValueSerializationType();
        
        // Key Serialization
        // For DEFAULT, we don't apply serializers
        if (keySerializationType == KafkaSerializationType.AVRO)
        {
            builder.SetKeySerializer(new AvroSerializer<TRecordKey>(BuildSchemaRegistryClient(options)).AsSyncOverAsync());
        }

        if (keySerializationType == KafkaSerializationType.JSON)
        {
            builder.SetKeySerializer(new JsonSerializer<TRecordKey>(BuildSchemaRegistryClient(options)).AsSyncOverAsync());
        }
        // End Key Serialization
        
        // Value Serialization
        // For DEFAULT, we don't apply serializers
        if (valueSerializationType == KafkaSerializationType.AVRO)
        {
            builder.SetValueSerializer(new AvroSerializer<TRecordValue>(BuildSchemaRegistryClient(options)).AsSyncOverAsync());
        }

        if (valueSerializationType == KafkaSerializationType.JSON)
        {
            builder.SetValueSerializer(new JsonSerializer<TRecordValue>(BuildSchemaRegistryClient(options)).AsSyncOverAsync());
        }
        // End Value Deserialization

        return builder;
    }
    
    
    #region Schema Registry Client
    
    
    private static ISchemaRegistryClient BuildSchemaRegistryClient(KafkaSchemaRegistryOptions config)
    {
        return new CachedSchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = config.Server,
            BasicAuthUserInfo = $"{config.User}:{config.Password}"
        });
    }
    
    #endregion

    #endregion

    #region IKafkaProducer
    
    public Task<DeliveryResult<TRecordKey, TRecordValue>> ProduceAsync(
        string topicName,
        Message<TRecordKey, TRecordValue> message,
        CancellationToken cancellationToken)
    {
        return Producer.ProduceAsync(topicName, message, cancellationToken);
    }
    
    #endregion
    
    #region Producer Handlers
    
    private ProducerBuilder<TRecordKey, TRecordValue> SetProducerHandlers(ProducerBuilder<TRecordKey, TRecordValue> producerBuilder)
    {
        producerBuilder.SetErrorHandler((_, e) => _logger.LogError("Produce Error: {Reason}", e.Reason)); // TODO: Revisit
        return producerBuilder;
    }

    private IProducer<TRecordKey, TRecordValue> Producer =>
        _producer ?? throw new InvalidOperationException("Kafka producer has not been initialized.");
    
    #endregion
}
