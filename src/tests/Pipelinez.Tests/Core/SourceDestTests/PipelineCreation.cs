using Pipelinez.Core;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.SourceDestTests;

public class PipelineCreation
{
    [Fact]
    public async void Pipeline_Builds_With_Source_Dest()
    {
        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_Builds_With_Source_Dest")
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            .Build();

        Assert.NotNull(pipeline);
    }
    
    [Fact]
    public async void Pipeline_Builds_With_Source_Segments_Dest()
    {
        var builder = Pipeline<TestPipelineRecord>.New("Pipeline_Builds_With_Source_Segments_Dest")
            .WithInMemorySource("config");

        var rand = new Random().Next(1,10);
        
        for(int i = 0; i < rand; i++)
        {
            builder.AddSegment(new TestSegment(), "config");
        }
            
        var pipeline = builder.WithInMemoryDestination("config")
            .Build();

        Assert.NotNull(pipeline);
    }
}