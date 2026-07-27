using ReValidation.Common.Models;

namespace ReValidation.Common.Abstractions;

public interface IRouteMetadataProvider
{
    ValidationRoute Route { get; }
    ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken);
}
