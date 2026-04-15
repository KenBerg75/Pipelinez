using Pipelinez.Core.Record;

namespace Pipelinez.AzureServiceBus.Tests.Models;

public sealed class TestAzureServiceBusRecord : PipelineRecord
{
    public string Id { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
