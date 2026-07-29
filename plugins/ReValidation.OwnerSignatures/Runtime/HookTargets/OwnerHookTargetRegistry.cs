namespace ReValidation.OwnerSignatures.Runtime.HookTargets;

public sealed class OwnerHookTargetRegistry(IEnumerable<OwnerHookTargetDefinition> definitions)
{
    private readonly Dictionary<string, OwnerHookTargetDefinition> definitions = definitions.ToDictionary(definition => definition.TargetId, StringComparer.Ordinal);

    public OwnerHookTargetDefinition Get(string targetId) =>
        definitions.TryGetValue(targetId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown owner hook target '{targetId}'.");
}
