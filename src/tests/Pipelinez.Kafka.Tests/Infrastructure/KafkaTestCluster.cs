using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Pipelinez.Kafka.Configuration;
using Testcontainers.Kafka;
using Xunit;

namespace Pipelinez.Kafka.Tests.Infrastructure;

public sealed class KafkaTestCluster : IAsyncLifetime, IAsyncDisposable
{
    private readonly KafkaTestClusterOptions _options;
    private readonly KafkaContainer _container;

    public KafkaTestCluster()
        : this(KafkaTestClusterOptions.LoadFromEnvironment())
    {
    }

    internal KafkaTestCluster(KafkaTestClusterOptions options)
    {
        _options = options;

        var builder = new KafkaBuilder(_options.KafkaImage);

        if (_options.ReuseContainer)
        {
            builder = builder.WithReuse(true);
        }

        _container = builder.Build();
    }

    public string BootstrapServers => _container.GetBootstrapAddress();

    public TimeSpan ConsumeTimeout => _options.ConsumeTimeout;

    public TimeSpan ObservationWindow => _options.ObservationWindow;

    public async Task InitializeAsync()
    {
        using var cancellation = new CancellationTokenSource(_options.StartupTimeout);
        await _container.StartAsync(cancellation.Token).ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        return DisposeAsyncCore().AsTask();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
    }

    public async Task<string> CreateTopicAsync(string scenarioName, int partitions = 1)
    {
        var topicName = KafkaTopicNameFactory.CreateTopicName(_options.TopicPrefix, scenarioName);
        await CreateTopicAsync(topicName, partitions, replicationFactor: 1).ConfigureAwait(false);
        return topicName;
    }

    public async Task CreateTopicAsync(string topicName, int partitions, short replicationFactor)
    {
        using var adminClient = new AdminClientBuilder(CreateAdminConfig()).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = topicName,
                    NumPartitions = partitions,
                    ReplicationFactor = replicationFactor
                }
            }).ConfigureAwait(false);
        }
        catch (CreateTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
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

    public async Task ProduceToPartitionAsync(string topicName, int partition, IEnumerable<Message<string, string>> messages)
    {
        using var producer = new ProducerBuilder<string, string>(CreateProducerConfig()).Build();
        var topicPartition = new TopicPartition(topicName, new Partition(partition));

        foreach (var message in messages)
        {
            await producer.ProduceAsync(topicPartition, message).ConfigureAwait(false);
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

    public async Task<IReadOnlyList<ConsumeResult<string, string>>> ConsumeAvailableAsync(
        string topicName,
        string consumerGroup,
        TimeSpan? observationWindow = null)
    {
        var results = new List<ConsumeResult<string, string>>();
        var deadline = DateTimeOffset.UtcNow + (observationWindow ?? _options.ObservationWindow);

        using var consumer = new ConsumerBuilder<string, string>(CreateConsumerConfig(consumerGroup)).Build();
        consumer.Subscribe(topicName);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(250));
            if (consumeResult?.Message is null || consumeResult.IsPartitionEOF)
            {
                await Task.Delay(50).ConfigureAwait(false);
                continue;
            }

            results.Add(consumeResult);
        }

        consumer.Close();
        return results;
    }

    public async Task<long?> WaitForCommittedOffsetAsync(
        string topicName,
        string consumerGroup,
        long minimumOffset,
        TimeSpan? timeout = null,
        int partition = 0)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? _options.ConsumeTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var committedOffset = GetCommittedOffset(topicName, consumerGroup, partition);
            if (committedOffset.HasValue && committedOffset.Value >= minimumOffset)
            {
                return committedOffset.Value;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        return null;
    }

    public long? GetCommittedOffset(string topicName, string consumerGroup, int partition = 0)
    {
        using var consumer = new ConsumerBuilder<string, string>(CreateConsumerConfig(consumerGroup)).Build();

        var committedOffset = consumer
            .Committed(
                new[] { new TopicPartition(topicName, new Partition(partition)) },
                TimeSpan.FromSeconds(5))
            .Single()
            .Offset;

        consumer.Close();

        return committedOffset == Offset.Unset
            ? null
            : committedOffset.Value;
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
            SecurityProtocol = SecurityProtocol.Plaintext,
            GroupId = consumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = false
        };
    }

    private ValueTask DisposeAsyncCore()
    {
        return _container.DisposeAsync();
    }
}
