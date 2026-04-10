using System.Threading.Tasks.Dataflow;

namespace Pipelinez.Core.Flow;

/// <summary>
/// Defines a source that can connect its output to a downstream flow destination.
/// </summary>
/// <typeparam name="TOutput">The type of messages produced by the source.</typeparam>
public interface IFlowSource<TOutput>
{
    /// <summary>
    /// Connects the source to a downstream destination.
    /// </summary>
    /// <param name="target">The destination that should receive published output.</param>
    /// <param name="options">Optional Dataflow link options applied to the connection.</param>
    /// <returns>A disposable link handle that can be used to disconnect the source from the destination.</returns>
    IDisposable ConnectTo(IFlowDestination<TOutput> target, DataflowLinkOptions? options = null);
}
