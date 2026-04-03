using Pipelinez.Core;
using Pipelinez.Core.Record;

namespace Pipelinez.Tests;

public class SomePipelineRecordClass : PipelineRecord
{
    public required string JobPosting { get; set; }
    public required string Key { get; set; }
    //public string Value { get; set; }
    
    public required string CSJobPosting { get; set; }
    
    
}
