using System.Threading.Tasks.Dataflow;

namespace Pipelinez.Core.Flow;

/// <summary>
/// Defines a source for a pipeline flow
/// </summary>
/// <typeparam name="TOutput">Type this flow will publish</typeparam>
public interface IFlowSource<TOutput>
{
    IDisposable ConnectTo(IFlowDestination<TOutput> target, DataflowLinkOptions? options = null);
    
}
