using Azure.Core;
using Pipelinez.AzureServiceBus.Configuration;
using Xunit;

namespace Pipelinez.AzureServiceBus.Tests.Unit;

public class AzureServiceBusOptionsTests
{
    [Fact]
    public void Connection_Options_Accept_Connection_String()
    {
        var options = new AzureServiceBusConnectionOptions
        {
            ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key"
        };

        Assert.Same(options, options.Validate());
    }

    [Fact]
    public void Connection_Options_Accept_Namespace_With_Credential()
    {
        var options = new AzureServiceBusConnectionOptions
        {
            FullyQualifiedNamespace = "example.servicebus.windows.net",
            Credential = new TestTokenCredential()
        };

        Assert.Same(options, options.Validate());
    }

    [Fact]
    public void Connection_Options_Reject_Mixed_Authentication_Styles()
    {
        var options = new AzureServiceBusConnectionOptions
        {
            ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key",
            FullyQualifiedNamespace = "example.servicebus.windows.net",
            Credential = new TestTokenCredential()
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Source_Rejects_Bare_Topic()
    {
        var options = CreateSourceOptions();
        options.Entity = AzureServiceBusEntityOptions.ForTopic("orders");

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Destination_Rejects_Topic_Subscription()
    {
        var options = new AzureServiceBusDestinationOptions
        {
            Connection = CreateConnection(),
            Entity = AzureServiceBusEntityOptions.ForTopicSubscription("orders", "workers")
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Source_Options_Accept_Queue_And_Topic_Subscription()
    {
        var queueOptions = CreateSourceOptions();
        queueOptions.Entity = AzureServiceBusEntityOptions.ForQueue("orders-in");

        var subscriptionOptions = CreateSourceOptions();
        subscriptionOptions.Entity = AzureServiceBusEntityOptions.ForTopicSubscription("orders", "workers");

        Assert.Same(queueOptions, queueOptions.Validate());
        Assert.Same(subscriptionOptions, subscriptionOptions.Validate());
    }

    [Fact]
    public void Source_Options_Validate_Concurrency_And_Prefetch()
    {
        var options = CreateSourceOptions();
        options.MaxConcurrentCalls = 0;

        Assert.Throws<InvalidOperationException>(() => options.Validate());

        options.MaxConcurrentCalls = 1;
        options.PrefetchCount = -1;

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    private static AzureServiceBusSourceOptions CreateSourceOptions()
    {
        return new AzureServiceBusSourceOptions
        {
            Connection = CreateConnection(),
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-in")
        };
    }

    private static AzureServiceBusConnectionOptions CreateConnection()
    {
        return new AzureServiceBusConnectionOptions
        {
            ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key"
        };
    }

    private sealed class TestTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(GetToken(requestContext, cancellationToken));
        }
    }
}
