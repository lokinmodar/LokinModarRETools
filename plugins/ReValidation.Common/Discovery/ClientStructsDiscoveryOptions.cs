namespace ReValidation.Common.Discovery;

public sealed record ClientStructsDiscoveryOptions
{
    public const string DefaultBaseRef = "upstream/main";

    public ClientStructsDiscoveryOptions(
        string repositoryPath,
        DiscoveryMode mode,
        IReadOnlyList<TargetFamily> families,
        string? targetFilter = null)
        : this(repositoryPath, DefaultBaseRef, mode, families, targetFilter)
    {
    }

    public ClientStructsDiscoveryOptions(
        string repositoryPath,
        string baseRef,
        DiscoveryMode mode,
        IReadOnlyList<TargetFamily> families,
        string? targetFilter)
    {
        RepositoryPath = repositoryPath;
        BaseRef = baseRef;
        Mode = mode;
        Families = families;
        TargetFilter = targetFilter;
    }

    public string RepositoryPath { get; }
    public string BaseRef { get; }
    public DiscoveryMode Mode { get; }
    public IReadOnlyList<TargetFamily> Families { get; }
    public string? TargetFilter { get; }
}
