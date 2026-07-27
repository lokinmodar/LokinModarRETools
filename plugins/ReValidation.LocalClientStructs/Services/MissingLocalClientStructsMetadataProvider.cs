using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;

namespace ReValidation.LocalClientStructs.Services;

public sealed class MissingLocalClientStructsMetadataProvider(string blockingReason) : IRouteMetadataProvider
{
    public ValidationRoute Route => ValidationRoute.LocalClientStructs;

    public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string?> metadata = new Dictionary<string, string?>
        {
            ["clientStructsAvailable"] = "false",
            ["clientStructsBlockingReason"] = blockingReason,
        };

        return ValueTask.FromResult(metadata);
    }
}
