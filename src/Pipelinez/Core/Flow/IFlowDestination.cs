using System.Threading.Tasks.Dataflow;

namespace Pipelinez.Core.Flow;

/// <summary>
/// Defines an interface that would be implemented by a target for a pipeline flow
/// </summary>
/// <typeparam name="TInput">Type this flow will accept</typeparam>
public interface IFlowDestination<TInput>
{
    ITargetBlock<TInput> AsTargetBlock();
}