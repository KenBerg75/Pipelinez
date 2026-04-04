using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Record;

namespace Pipelinez.Tests.Core.DeadLetterTests.Models;

public sealed class ThrowingDeadLetterDestination<T> : IPipelineDeadLetterDestination<T>
    where T : PipelineRecord
{
    private readonly Exception _exception;

    public ThrowingDeadLetterDestination(Exception exception)
    {
        _exception = exception;
    }

    public Task WriteAsync(PipelineDeadLetterRecord<T> deadLetterRecord, CancellationToken cancellationToken)
    {
        throw _exception;
    }
}
