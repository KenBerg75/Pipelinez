namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures terminal settlement behavior for S3 source objects.
/// </summary>
public sealed class AmazonS3ObjectSettlementOptions
{
    /// <summary>
    /// Gets the settlement action applied after successful processing.
    /// </summary>
    public AmazonS3ObjectSettlementAction OnSuccess { get; init; } = AmazonS3ObjectSettlementAction.Leave;

    /// <summary>
    /// Gets the settlement action applied after Pipelinez dead-letter handling succeeds.
    /// </summary>
    public AmazonS3ObjectSettlementAction OnDeadLetter { get; init; } = AmazonS3ObjectSettlementAction.Leave;

    /// <summary>
    /// Gets the settlement action applied after a record is skipped.
    /// </summary>
    public AmazonS3ObjectSettlementAction OnSkipped { get; init; } = AmazonS3ObjectSettlementAction.Leave;

    /// <summary>
    /// Gets the target prefix used for success copy or move settlement.
    /// </summary>
    public string? SuccessPrefix { get; init; }

    /// <summary>
    /// Gets the target prefix used for dead-letter copy or move settlement.
    /// </summary>
    public string? DeadLetterPrefix { get; init; }

    /// <summary>
    /// Gets the target prefix used for skipped-record copy or move settlement.
    /// </summary>
    public string? SkippedPrefix { get; init; }

    /// <summary>
    /// Gets tags applied for success tag settlement.
    /// </summary>
    public IReadOnlyDictionary<string, string> SuccessTags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets tags applied for dead-letter tag settlement.
    /// </summary>
    public IReadOnlyDictionary<string, string> DeadLetterTags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets tags applied for skipped-record tag settlement.
    /// </summary>
    public IReadOnlyDictionary<string, string> SkippedTags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Validates the settlement options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated settlement options.</returns>
    public AmazonS3ObjectSettlementOptions Validate()
    {
        ValidateAction(OnSuccess, nameof(OnSuccess), SuccessPrefix, SuccessTags);
        ValidateAction(OnDeadLetter, nameof(OnDeadLetter), DeadLetterPrefix, DeadLetterTags);
        ValidateAction(OnSkipped, nameof(OnSkipped), SkippedPrefix, SkippedTags);
        return this;
    }

    internal AmazonS3ObjectSettlementDecision GetSuccessDecision()
    {
        return CreateDecision(OnSuccess, SuccessPrefix, SuccessTags);
    }

    internal AmazonS3ObjectSettlementDecision GetDeadLetterDecision()
    {
        return CreateDecision(OnDeadLetter, DeadLetterPrefix, DeadLetterTags);
    }

    internal AmazonS3ObjectSettlementDecision GetSkippedDecision()
    {
        return CreateDecision(OnSkipped, SkippedPrefix, SkippedTags);
    }

    private static AmazonS3ObjectSettlementDecision CreateDecision(
        AmazonS3ObjectSettlementAction action,
        string? prefix,
        IReadOnlyDictionary<string, string> tags)
    {
        return new AmazonS3ObjectSettlementDecision(action, prefix, tags);
    }

    private static void ValidateAction(
        AmazonS3ObjectSettlementAction action,
        string actionName,
        string? prefix,
        IReadOnlyDictionary<string, string> tags)
    {
        if (!Enum.IsDefined(action))
        {
            throw new InvalidOperationException($"Unsupported Amazon S3 settlement action '{action}'.");
        }

        if (action is AmazonS3ObjectSettlementAction.Copy or AmazonS3ObjectSettlementAction.Move &&
            string.IsNullOrWhiteSpace(prefix))
        {
            throw new InvalidOperationException($"{actionName} requires a target prefix when using copy or move settlement.");
        }

        if (action == AmazonS3ObjectSettlementAction.Tag && tags.Count == 0)
        {
            throw new InvalidOperationException($"{actionName} requires at least one tag when using tag settlement.");
        }

        ValidatePrefix(prefix);
        ValidateTags(tags);
    }

    private static void ValidatePrefix(string? prefix)
    {
        if (prefix is not null && prefix.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException("Amazon S3 settlement prefix cannot contain control characters.");
        }
    }

    private static void ValidateTags(IReadOnlyDictionary<string, string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Key))
            {
                throw new InvalidOperationException("Amazon S3 settlement tag keys cannot be empty.");
            }

            ArgumentNullException.ThrowIfNull(tag.Value);
        }
    }
}
