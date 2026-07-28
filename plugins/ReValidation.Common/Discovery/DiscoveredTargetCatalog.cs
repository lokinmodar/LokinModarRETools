namespace ReValidation.Common.Discovery;

public sealed record DiscoveredTargetCatalog
{
    public DiscoveredTargetCatalog(
        ClientStructsDiscoveryOptions options,
        IReadOnlyList<DiscoveredTarget> targets,
        IReadOnlyList<string> warnings)
    {
        Options = options;
        Targets = targets;
        Warnings = warnings;
    }

    public ClientStructsDiscoveryOptions Options { get; }
    public IReadOnlyList<DiscoveredTarget> Targets { get; }
    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<DiscoveredTarget> Filter(TargetFamily family, string? targetPrefix) =>
        Targets
            .Where(target => target.Family == family)
            .Where(target => string.IsNullOrWhiteSpace(targetPrefix) || target.TargetId.StartsWith(targetPrefix, StringComparison.Ordinal))
            .ToArray();
}
