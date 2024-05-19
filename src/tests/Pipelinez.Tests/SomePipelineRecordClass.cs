using Pipelinez.Core;
using Pipelinez.Core.Record;

namespace Pipelinez.Tests;

public class SomePipelineRecordClass : PipelineRecord
{
    public string JobPosting { get; set; }
    public string Key { get; set; }
    //public string Value { get; set; }
    
    public string CSJobPosting { get; set; }
    
    
}