using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;

namespace Pipelinez.Core.Performance;

/// <summary>
/// Configures execution behavior for a source, segment, or destination.
/// </summary>
public sealed class PipelineExecutionOptions
{
    /// <summary>
    /// Creates the default execution options used for sources.
    /// </summary>
    /// <returns>The default source execution options.</returns>
    public static PipelineExecutionOptions CreateDefaultSourceOptions()
    {
        return new PipelineExecutionOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded,
            DegreeOfParallelism = 1,
            EnsureOrdered = true
        };
    }

    /// <summary>
    /// Creates the default execution options used for segments.
    /// </summary>
    /// <returns>The default segment execution options.</returns>
    public static PipelineExecutionOptions CreateDefaultSegmentOptions()
    {
        return new PipelineExecutionOptions
        {
            BoundedCapacity = 10_000,
            DegreeOfParallelism = 1,
            EnsureOrdered = true
        };
    }

    /// <summary>
    /// Creates the default execution options used for destinations.
    /// </summary>
    /// <returns>The default destination execution options.</returns>
    public static PipelineExecutionOptions CreateDefaultDestinationOptions()
    {
        return new PipelineExecutionOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded,
            DegreeOfParallelism = 1,
            EnsureOrdered = true
        };
    }

    /// <summary>
    /// Gets the bounded capacity used by the underlying Dataflow block.
    /// </summary>
    public int BoundedCapacity { get; init; } = DataflowBlockOptions.Unbounded;

    /// <summary>
    /// Gets the maximum degree of parallelism used by the component.
    /// </summary>
    public int DegreeOfParallelism { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether record ordering should be preserved.
    /// </summary>
    public bool EnsureOrdered { get; init; } = true;

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public PipelineExecutionOptions Validate()
    {
        if (BoundedCapacity != DataflowBlockOptions.Unbounded && BoundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BoundedCapacity),
                BoundedCapacity,
                "Bounded capacity must be greater than zero or DataflowBlockOptions.Unbounded.");
        }

        Guard.Against.NegativeOrZero(DegreeOfParallelism, nameof(DegreeOfParallelism));
        return this;
    }

    /// <summary>
    /// Determines whether these options match another execution options instance.
    /// </summary>
    /// <param name="other">The options to compare.</param>
    /// <returns><see langword="true" /> when the options are equivalent; otherwise, <see langword="false" />.</returns>
    public bool Matches(PipelineExecutionOptions? other)
    {
        return other is not null &&
               BoundedCapacity == other.BoundedCapacity &&
               DegreeOfParallelism == other.DegreeOfParallelism &&
               EnsureOrdered == other.EnsureOrdered;
    }
}
