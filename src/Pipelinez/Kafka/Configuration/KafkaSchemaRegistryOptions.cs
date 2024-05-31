namespace Pipelinez.Kafka.Configuration;

public class KafkaSchemaRegistryOptions
{
    private string _server = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string KeySerializer { get; set; } = string.Empty;
    public string ValueSerializer { get; set; } = string.Empty;
    
    public string Server
    {
        get => GetSchemaServer();
        set => _server = value;
    }
    
    #region Helpers
    private string GetSchemaServer()
    {
        return _server.StartsWith("https://")
            ? _server
            : $"{"https://"}{_server}";
    }

    #region Serialization
    internal KafkaSerializationType GetKeySerializationType()
    {
        if (string.IsNullOrEmpty(KeySerializer))
        { return KafkaSerializationType.DEFAULT; }

        // Avro
        if (KeySerializer.Equals("AVRO", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaSerializationType.AVRO; }
        
        // Json
        if (KeySerializer.Equals("JSON", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaSerializationType.JSON; }

        return KafkaSerializationType.DEFAULT;
    }
    
    internal KafkaSerializationType GetValueSerializationType()
    {
        if (string.IsNullOrEmpty(ValueSerializer))
        { return KafkaSerializationType.DEFAULT; }

        // Avro
        if (ValueSerializer.Equals("AVRO", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaSerializationType.AVRO; }
        
        // Json
        if (ValueSerializer.Equals("JSON", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaSerializationType.JSON; }

        return KafkaSerializationType.DEFAULT;
    }
    
    #endregion
    
    #region DeSerialization
    
    internal KafkaDeserializationType GetKeyDeserializationType()
    {
        if (string.IsNullOrEmpty(KeySerializer))
        { return KafkaDeserializationType.DEFAULT; }

        // Avro
        if (KeySerializer.Equals("AVRO", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaDeserializationType.AVRO; }
        
        // Json
        if (KeySerializer.Equals("JSON", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaDeserializationType.JSON; }

        return KafkaDeserializationType.DEFAULT;
    }
    
    internal KafkaDeserializationType GetValueDeserializationType()
    {
        if (string.IsNullOrEmpty(ValueSerializer))
        { return KafkaDeserializationType.DEFAULT; }

        // Avro
        if (ValueSerializer.Equals("AVRO", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaDeserializationType.AVRO; }
        
        // Json
        if (ValueSerializer.Equals("JSON", StringComparison.InvariantCultureIgnoreCase))
        { return KafkaDeserializationType.JSON; }

        return KafkaDeserializationType.DEFAULT;
    }
    
    #endregion
    
    #endregion
}