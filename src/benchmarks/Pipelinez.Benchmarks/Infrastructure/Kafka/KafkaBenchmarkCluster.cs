using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Pipelinez.Kafka.Configuration;
using Testcontainers.Kafka;

namespace Pipelinez.Benchmarks;

internal sealed class KafkaBenchmarkCluster : IAsyncDisposable
{
    private readonly KafkaBenchmarkClusterOptions _options;
    private readonly KafkaContainer _container;

    public KafkaBenchmarkCluster()
        : this(KafkaBenchmarkClusterOptions.LoadFromEnvironment())
    {
    }

    private KafkaBenchmarkCluster(KafkaBenchmarkClusterOptions options)
    {
        _options = options;

        var builder = new KafkaBuilder(options.KafkaImage);
        if (options.ReuseContainer)
        {
            builder = builder.WithReuse(true);
        }

        _container = builder.Build();
    }

    public string BootstrapServers => _container.GetBootstrapAddress();

    public async Task InitializeAsync()
    {
        using var cancellation = new CancellationTokenSource(_options.StartupTimeout);
        await _container.StartAsync(cancellation.Token).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    public async Task<string> CreateTopicAsync(string benchmarkName, string suffix, int partitions = 1)
    {
        var topicName = BenchmarkScenarioNameFactory.CreateKafkaTopicName(benchmarkName, suffix);
        await CreateTopicAsync(topicName, partitions, replicationFactor: 1).ConfigureAwait(false);
        return topicName;
    }

    public async Task CreateTopicAsync(string topicName, int partitions, short replicationFactor)
    {
        using var adminClient = new AdminClientBuilder(CreateAdminConfig()).Build();

        try
        {
            await adminClient.CreateTopicsAsync(
                    [
                        new TopicSpecification
                        {
                            Name = topicName,
                            NumPartitions = partitions,
                            ReplicationFactor = replicationFactor
                        }
                    ])
                .ConfigureAwait(false);
        }
        catch (CreateTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }

    public async Task DeleteTopicAsync(string topicName)
    {
        using var adminClient = new AdminClientBuilder(CreateAdminConfig()).Build();

        try
        {
            await adminClient.DeleteTopicsAsync([topicName]).ConfigureAwait(false);
        }
        catch (DeleteTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.UnknownTopicOrPart))
        {
        }
    }

    public async Task ProduceAsync(string topicName, IEnumerable<Message<string, string>> messages)
    {
        using var producer = new ProducerBuilder<string, string>(CreateProducerConfig()).Build();

        foreach (var message in messages)
        {
            await producer.ProduceAsync(topicName, message).ConfigureAwait(false);
        }

        producer.Flush(TimeSpan.FromSeconds(10));
    }

    public async Task<IReadOnlyList<ConsumeResult<string, string>>> ConsumeAsync(
        string topicName,
        int expectedCount,
        string consumerGroup,
        TimeSpan? timeout = null)
    {
        var results = new List<ConsumeResult<string, string>>();
        var deadline = DateTimeOffset.UtcNow + (timeout ?? _options.ConsumeTimeout);

        using var consumer = new ConsumerBuilder<string, string>(CreateConsumerConfig(consumerGroup)).Build();
        consumer.Subscribe(topicName);

        while (DateTimeOffset.UtcNow < deadline && results.Count < expectedCount)
        {
            var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(250));
            if (consumeResult?.Message is null || consumeResult.IsPartitionEOF)
            {
                continue;
            }

            results.Add(consumeResult);
            await Task.Yield();
        }

        consumer.Close();
        return results;
    }

    public KafkaSourceOptions CreateSourceOptions(string topicName, string consumerGroup)
    {
        return new KafkaSourceOptions
        {
            BootstrapServers = BootstrapServers,
            TopicName = topicName,
            ConsumerGroup = consumerGroup,
            SecurityProtocol = SecurityProtocol.Plaintext,
            StartOffsetFromBeginning = true,
            AutoCommitIntervalMs = _options.AutoCommitIntervalMs
        };
    }

    public KafkaDestinationOptions CreateDestinationOptions(string topicName)
    {
        return new KafkaDestinationOptions
        {
            BootstrapServers = BootstrapServers,
            TopicName = topicName,
            SecurityProtocol = SecurityProtocol.Plaintext
        };
    }

    private AdminClientConfig CreateAdminConfig()
    {
        return new AdminClientConfig
        {
            BootstrapServers = BootstrapServers,
            SecurityProtocol = SecurityProtocol.Plaintext
        };
    }

    private ProducerConfig CreateProducerConfig()
    {
        return new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            SecurityProtocol = SecurityProtocol.Plaintext
        };
    }

    private ConsumerConfig CreateConsumerConfig(string consumerGroup)
    {
        return new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = consumerGroup,
            SecurityProtocol = SecurityProtocol.Plaintext,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
    }
}
