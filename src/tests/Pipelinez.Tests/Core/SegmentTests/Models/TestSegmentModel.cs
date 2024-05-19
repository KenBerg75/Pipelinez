using Pipelinez.Core;
using Pipelinez.Core.Record;

namespace Pipelinez.Tests.Core.SegmentTests.Models;

public class TestSegmentModel : PipelineRecord
{
    public int FirstValue { get; set; }
    public int SecondValue { get; set; }
    public int AddResult { get; set; }
    
    public int MultiplyResult { get; set; }
}