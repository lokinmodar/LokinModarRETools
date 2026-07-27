using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;

namespace ReValidation.LocalClientStructs.Services;

public sealed class RealLocalClientStructsMetadataProvider(
    string branch,
    string commit,
    bool isDirty,
    string assemblySha256) : IRouteMetadataProvider
{
    public ValidationRoute Route => ValidationRoute.LocalClientStructs;

    public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string?> metadata = new Dictionary<string, string?>
        {
            ["clientStructsBranch"] = branch,
            ["clientStructsCommit"] = commit,
            ["clientStructsDirty"] = isDirty.ToString().ToLowerInvariant(),
            ["clientStructsAssemblySha256"] = assemblySha256,
        };

        return ValueTask.FromResult(metadata);
    }
}
