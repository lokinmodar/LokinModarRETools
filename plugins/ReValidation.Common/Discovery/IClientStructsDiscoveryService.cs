namespace ReValidation.Common.Discovery;

public interface IClientStructsDiscoveryService
{
    ValueTask<DiscoveredTargetCatalog> DiscoverAsync(ClientStructsDiscoveryOptions options, CancellationToken cancellationToken);
}
