using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;

namespace ReValidation.LocalClientStructs.Services;

public sealed class MissingLocalClientStructsMetadataProvider : IRouteMetadataProvider
{
    private static readonly HashSet<string> AllowedBlockingReasons =
    [
        "plugins/local/LocalClientStructs.props is missing.",
        "ClientStructsProjectPath could not be resolved.",
    ];
    private readonly string blockingReason;

    public MissingLocalClientStructsMetadataProvider(string blockingReason)
    {
        if (!AllowedBlockingReasons.Contains(blockingReason))
            throw new ArgumentOutOfRangeException(nameof(blockingReason));

        this.blockingReason = blockingReason;
    }

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
