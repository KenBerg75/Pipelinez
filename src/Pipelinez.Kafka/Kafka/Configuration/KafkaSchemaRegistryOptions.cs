namespace Pipelinez.Kafka.Configuration;

/// <summary>
/// Configures Confluent Schema Registry connectivity and serializer selection for Kafka transports.
/// </summary>
public class KafkaSchemaRegistryOptions
{
    private string _server = string.Empty;

    /// <summary>
    /// Gets or sets the Schema Registry username.
    /// </summary>
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Schema Registry password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configured key serializer name, such as <c>AVRO</c> or <c>JSON</c>.
    /// </summary>
    public string KeySerializer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configured value serializer name, such as <c>AVRO</c> or <c>JSON</c>.
    /// </summary>
    public string ValueSerializer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Schema Registry server address.
    /// </summary>
    /// <remarks>
    /// When an <c>https://</c> prefix is not provided, one is added automatically when the value is read.
    /// </remarks>
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
