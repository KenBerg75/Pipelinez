using Pipelinez.Core;
using Pipelinez.Core.Record;

namespace Pipelinez.Tests.Core.SegmentTests.Models;

public class TestOrderModel : PipelineRecord
{
    public DateTime FirstStamp { get; set; }
    public DateTime SecondStamp { get; set; }
}