using Pipelinez.Core.Destination;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DestinationTests.Models;

public sealed class BlockingAsyncDestination : PipelineDestination<TestPipelineRecord>
{
    private readonly TaskCompletionSource _allowCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ExecutionStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ExecutionCompleted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task ExecuteAsync(TestPipelineRecord record, CancellationToken cancellationToken)
    {
        ExecutionStarted.TrySetResult();
        await _allowCompletion.Task.WaitAsync(cancellationToken);
        ExecutionCompleted.TrySetResult();
    }

    public void ReleaseExecution()
    {
        _allowCompletion.TrySetResult();
    }

    protected override void Initialize()
    {
    }
}
