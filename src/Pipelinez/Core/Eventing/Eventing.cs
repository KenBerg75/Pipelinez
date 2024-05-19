namespace Pipelinez.Core.Eventing;


public delegate void PipelineContainerCompletedEventHandler<T>(object sender,
    PipelineContainerCompletedEventHandlerArgs<T> args);

/// <summary>
/// Contains data for the PipelineRecordCompleted event.
/// </summary>
/// <typeparam name="T"></typeparam>
public class PipelineContainerCompletedEventHandlerArgs<T>
{
    public T Container { get; private set; }
    
    public PipelineContainerCompletedEventHandlerArgs(T container)
    {
        Container = container;
    }
}

public delegate void PipelineRecordCompletedEventHandler<T>(object sender, PipelineRecordCompletedEventHandlerArgs<T> args);

/// <summary>
/// Contains data for the PipelineRecordCompleted event.
/// </summary>
/// <typeparam name="T"></typeparam>
public class PipelineRecordCompletedEventHandlerArgs<T>
{
    public T Record { get; private set; }
    
    public PipelineRecordCompletedEventHandlerArgs(T record)
    {
        Record = record;
    }
}