using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;

namespace ReValidation.OwnerSignatures.Services;

public sealed class OwnerSignatureMetadataProvider(IEnumerable<SignatureResolution> resolutions) : IRouteMetadataProvider
{
    private readonly IReadOnlyList<SignatureResolution> resolutions = resolutions.ToArray();

    public ValidationRoute Route => ValidationRoute.OwnerSignatures;

    public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string?> metadata = resolutions.ToDictionary(
            x => $"signature:{x.Id}",
            x => (string?)(x.Rva is null
                ? $"{x.MatchCount} matches"
                : $"matchCount={x.MatchCount};rva=0x{x.Rva:X}"),
            StringComparer.Ordinal);

        return ValueTask.FromResult(metadata);
    }
}
