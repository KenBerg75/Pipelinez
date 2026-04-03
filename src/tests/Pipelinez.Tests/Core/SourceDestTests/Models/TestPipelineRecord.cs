using Pipelinez.Core;
using Pipelinez.Core.Record;

namespace Pipelinez.Tests.Core.SourceDestTests.Models;

public class TestPipelineRecord : PipelineRecord
{
    public required string Data { get; set; }
}
