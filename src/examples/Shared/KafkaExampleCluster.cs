using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Pipelinez.Kafka.Configuration;
using Testcontainers.Kafka;

namespace Examples.Shared;

public sealed class KafkaExampleCluster : IAsyncDisposable
{
    private readonly KafkaExampleSettings _settings;
    private readonly KafkaContainer? _container;

    private KafkaExampleCluster(KafkaExampleSettings settings, KafkaContainer? container)
    {
        _settings = settings;
        _container = container;
    }

    public string BootstrapServers =>
        _settings.BootstrapServersOverride ?? _container?.GetBootstrapAddress()
        ?? throw new InvalidOperationException("Kafka bootstrap servers are unavailable.");

    public string SourceTopic => _settings.SourceTopic;

    public string DestinationTopic => _settings.DestinationTopic;

    public string ConsumerGroup => _settings.ConsumerGroup;

    public int MessageCount => _settings.MessageCount;

    public static async Task<KafkaExampleCluster> StartAsync(KafkaExampleSettings settings, CancellationToken cancellationToken = default)
    {
        KafkaContainer? container = null;

        if (settings.ShouldStartContainer)
        {
            var builder = new KafkaBuilder(settings.KafkaImage);
            if (settings.ReuseContainer)
            {
                builder = builder.WithReuse(true);
            }

            container = builder.Build();

            using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startupCancellation.CancelAfter(settings.StartupTimeout);
            await container.StartAsync(startupCancellation.Token).ConfigureAwait(false);
        }

        return new KafkaExampleCluster(settings, container);
    }

    public async Task EnsureTopicsAsync(CancellationToken cancellationToken = default)
    {
        var topics = new[]
        {
            SourceTopic,
            DestinationTopic
        };

        using var adminClient = new AdminClientBuilder(CreateAdminConfig()).Build();

        foreach (var topic in topics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await adminClient.CreateTopicsAsync(new[]
                {
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }
                }).ConfigureAwait(false);
            }
            catch (CreateTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
            }
        }
    }

    public KafkaSourceOptions CreateSourceOptions()
    {
        return new KafkaSourceOptions
        {
            BootstrapServers = BootstrapServers,
            TopicName = SourceTopic,
            ConsumerGroup = ConsumerGroup,
            SecurityProtocol = SecurityProtocol.Plaintext,
            StartOffsetFromBeginning = true,
            AutoCommitIntervalMs = 250
        };
    }

    public KafkaDestinationOptions CreateDestinationOptions()
    {
        return new KafkaDestinationOptions
        {
            BootstrapServers = BootstrapServers,
            TopicName = DestinationTopic,
            SecurityProtocol = SecurityProtocol.Plaintext
        };
    }

    public async Task ProduceAsync(IEnumerable<Message<string, string>> messages, CancellationToken cancellationToken = default)
    {
        using var producer = new ProducerBuilder<string, string>(CreateProducerConfig()).Build();

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await producer.ProduceAsync(SourceTopic, message, cancellationToken).ConfigureAwait(false);
        }

        producer.Flush(TimeSpan.FromSeconds(10));
    }

    public ValueTask DisposeAsync()
    {
        return _container?.DisposeAsync() ?? ValueTask.CompletedTask;
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
}
