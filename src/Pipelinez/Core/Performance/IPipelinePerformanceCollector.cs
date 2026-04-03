namespace Pipelinez.Core.Performance;

internal interface IPipelinePerformanceCollector
{
    void RecordPublished(string componentName);

    void RecordCompleted(DateTimeOffset createdAtUtc);

    void RecordFaulted(DateTimeOffset createdAtUtc);

    void RecordComponentExecution(string componentName, TimeSpan duration, bool succeeded);

    void RecordRetryAttempt();

    void RecordRetryRecovery();

    void RecordRetryExhausted();

    void RecordPublishWait(TimeSpan waitDuration);

    void RecordPublishRejected();

    void ObserveBufferedCount(int totalBufferedCount);

    PipelinePerformanceSnapshot CreateSnapshot();
}
