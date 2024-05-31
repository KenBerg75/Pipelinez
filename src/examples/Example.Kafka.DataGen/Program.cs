using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Newtonsoft.Json;
using Pipelinez.Kafka.Configuration;

uint howMany = 10;

Console.WriteLine($"Publishing {howMany} Records...");

await Go(howMany);

Console.WriteLine("Done.");

#region Go
static async Task Go(uint count)
{
    var finalConfig = new ProducerConfig()
    {
        BootstrapServers = "",
        SaslUsername = "",
        SaslPassword = "",
        
        
        SaslMechanism = SaslMechanism.Plain, 
        SecurityProtocol = SecurityProtocol.SaslSsl,
        ClientId = ""
    };
        
    var producer = new ProducerBuilder<string, string>(finalConfig).Build();
    static void DeliveryHandler(DeliveryReport<string, string> r)
    {
        if (!r.Error.IsError)
        {
            Console.WriteLine("Delivered");
        }
        else
        {
            Console.WriteLine($"Delivery Error: {r.Error.Reason}");
        }
    }
    for (int i = 0; i < count; i++)
    {
        try
        {
            await producer.ProduceAsync("", new Message<string, string> { Key = $"Key:{i}", Value = $"Value:{i}"}, new CancellationToken());
            //Thread.Sleep(250);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            break;
        }
    }

}

#endregion

#region Utility

static ISchemaRegistryClient BuildSchemaRegistryClient(KafkaSchemaRegistryOptions config)
{
    return new CachedSchemaRegistryClient(new SchemaRegistryConfig
    {
        Url = config.Server,
        BasicAuthUserInfo = $"{config.User}:{config.Password}"
    });
}

#endregion