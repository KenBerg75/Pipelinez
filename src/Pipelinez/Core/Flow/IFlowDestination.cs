using System.Threading.Tasks.Dataflow;

namespace Pipelinez.Core.Flow;

/// <summary>
/// Defines a destination that can accept input from an upstream flow source.
/// </summary>
/// <typeparam name="TInput">The type of messages accepted by the destination.</typeparam>
public interface IFlowDestination<TInput>
{
    /// <summary>
    /// Gets the underlying Dataflow target block used to receive messages.
    /// </summary>
    /// <returns>The target block that should receive linked input.</returns>
    ITargetBlock<TInput> AsTargetBlock();
}
