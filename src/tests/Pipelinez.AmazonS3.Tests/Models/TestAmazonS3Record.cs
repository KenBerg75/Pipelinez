using Pipelinez.Core.Record;

namespace Pipelinez.AmazonS3.Tests.Models;

public sealed class TestAmazonS3Record : PipelineRecord
{
    public required string Id { get; init; }

    public required string Value { get; init; }
}
