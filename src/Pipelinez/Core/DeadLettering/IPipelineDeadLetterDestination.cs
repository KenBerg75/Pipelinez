using Pipelinez.Core.Record;

namespace Pipelinez.Core.DeadLettering;

public interface IPipelineDeadLetterDestination<T> where T : PipelineRecord
{
    Task WriteAsync(PipelineDeadLetterRecord<T> deadLetterRecord, CancellationToken cancellationToken);
}
