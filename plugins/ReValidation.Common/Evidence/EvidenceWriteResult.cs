namespace ReValidation.Common.Evidence;

public sealed record EvidenceWriteResult(
    string Kind,
    string OutputPath,
    bool IsSuccess = true,
    string? FailureReason = null)
{
    public static EvidenceWriteResult Failed(string kind) =>
        new(kind, string.Empty, false, "Evidence export failed.");
}
