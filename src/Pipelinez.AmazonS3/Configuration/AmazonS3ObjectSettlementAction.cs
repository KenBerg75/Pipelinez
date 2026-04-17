namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Describes how an S3 source object should be settled after terminal pipeline handling.
/// </summary>
public enum AmazonS3ObjectSettlementAction
{
    /// <summary>
    /// Leave the source object unchanged.
    /// </summary>
    Leave,

    /// <summary>
    /// Delete the source object.
    /// </summary>
    Delete,

    /// <summary>
    /// Apply configured object tags to the source object.
    /// </summary>
    Tag,

    /// <summary>
    /// Copy the source object to a configured target prefix and leave the original object.
    /// </summary>
    Copy,

    /// <summary>
    /// Copy the source object to a configured target prefix and then delete the original object.
    /// </summary>
    Move
}
