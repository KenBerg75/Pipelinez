using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Eventing;


public delegate void PipelineContainerCompletedEventHandler<T>(object sender,
    PipelineContainerCompletedEventHandlerArgs<T> args);

/// <summary>
/// Contains data for the PipelineRecordCompleted event.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class PipelineContainerCompletedEventHandlerArgs<T>
{
    public T Container { get; }
    
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
public sealed class PipelineRecordCompletedEventHandlerArgs<T>
{
    public T Record { get; }
    
    public PipelineRecordCompletedEventHandlerArgs(T record)
    {
        Record = record;
    }
}

public delegate void PipelineRecordFaultedEventHandler<T>(
    object sender,
    PipelineRecordFaultedEventArgs<T> args) where T : PipelineRecord;

public sealed class PipelineRecordFaultedEventArgs<T> where T : PipelineRecord
{
    public PipelineRecordFaultedEventArgs(
        T record,
        PipelineContainer<T> container,
        PipelineFaultState fault)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Container = Guard.Against.Null(container, nameof(container));
        Fault = Guard.Against.Null(fault, nameof(fault));
    }

    public T Record { get; }

    public PipelineContainer<T> Container { get; }

    public PipelineFaultState Fault { get; }
}

public delegate void PipelineFaultedEventHandler(object sender, PipelineFaultedEventArgs args);

public sealed class PipelineFaultedEventArgs
{
    public PipelineFaultedEventArgs(string pipelineName, PipelineFaultState fault)
    {
        PipelineName = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        Fault = Guard.Against.Null(fault, nameof(fault));
    }

    public string PipelineName { get; }

    public PipelineFaultState Fault { get; }

    public Exception Exception => Fault.Exception;

    public string ComponentName => Fault.ComponentName;

    public PipelineComponentKind ComponentKind => Fault.ComponentKind;
}
