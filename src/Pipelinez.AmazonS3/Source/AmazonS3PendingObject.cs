using Pipelinez.AmazonS3.Configuration;

namespace Pipelinez.AmazonS3.Source;

internal sealed class AmazonS3PendingObject
{
    private readonly TaskCompletionSource<AmazonS3ObjectSettlementDecision> _settlement =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AmazonS3PendingObject(
        string settlementKey,
        string objectKey,
        string? versionId)
    {
        SettlementKey = settlementKey;
        ObjectKey = objectKey;
        VersionId = versionId;
    }

    public string SettlementKey { get; }

    public string ObjectKey { get; }

    public string? VersionId { get; }

    public Task<AmazonS3ObjectSettlementDecision> Settlement => _settlement.Task;

    public void TrySetSettlement(AmazonS3ObjectSettlementDecision decision)
    {
        _settlement.TrySetResult(decision);
    }

    public void TryCancel()
    {
        _settlement.TrySetCanceled();
    }
}
