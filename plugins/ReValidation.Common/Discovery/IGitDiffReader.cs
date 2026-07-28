namespace ReValidation.Common.Discovery;

public interface IGitDiffReader
{
    ValueTask<IReadOnlyList<string>> ReadChangedFilesAsync(
        string repositoryPath,
        string baseRef,
        CancellationToken cancellationToken);
}
