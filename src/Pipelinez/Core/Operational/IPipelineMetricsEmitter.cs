namespace Pipelinez.Core.Operational;

internal interface IPipelineMetricsEmitter : IDisposable
{
    void RecordPublished();

    void RecordCompleted();

    void RecordFaulted();

    void RecordRetryAttempt();

    void RecordRetryRecovery();

    void RecordRetryExhausted();

    void RecordDeadLettered();

    void RecordDeadLetterFailure();

    void RecordPublishRejected();

    void ObserveBufferedCount(int totalBufferedCount);

    void ObserveOwnedPartitionCount(int ownedPartitionCount);
}
