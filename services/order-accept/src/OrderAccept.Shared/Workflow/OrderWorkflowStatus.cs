namespace OrderAccept.Shared.Workflow;

/// <summary>
/// Specifies the status of an order within the workflow process.
/// </summary>
/// <remarks>Use this enumeration to track and manage the progression of an order through its various workflow
/// stages, such as when it is accepted, being processed, or completed. The values are intended to represent distinct,
/// sequential phases in the order lifecycle.</remarks>
public enum OrderWorkflowStatus
{
    /// <summary>
    /// Indicates that the request has been accepted for processing, but the processing has not been completed.
    /// </summary>
    /// <remarks>This status code corresponds to HTTP 202 Accepted. It is typically used to indicate that the
    /// request has been received and understood, but that processing will occur asynchronously.</remarks>
    Accepted = 1,

    /// <summary>
    /// Indicates that the operation is currently in progress.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Indicates that the operation has completed successfully.
    /// </summary>
    Completed = 3
}
