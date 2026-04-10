namespace Pipelinez.Core.Performance;

/// <summary>
/// Defines a component whose execution behavior can be tuned with Pipelinez execution options.
/// </summary>
public interface IPipelineExecutionConfigurable
{
    /// <summary>
    /// Applies execution options to the component before it begins processing.
    /// </summary>
    /// <param name="options">The execution options to apply.</param>
    void ConfigureExecutionOptions(PipelineExecutionOptions options);

    /// <summary>
    /// Gets the execution options currently configured for the component.
    /// </summary>
    /// <returns>The active execution options.</returns>
    PipelineExecutionOptions GetExecutionOptions();
}
