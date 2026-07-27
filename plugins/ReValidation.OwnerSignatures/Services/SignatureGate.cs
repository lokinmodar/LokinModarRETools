namespace ReValidation.OwnerSignatures.Services;

public sealed class SignatureGate
{
    public SignatureGateResult Evaluate(
        IEnumerable<SignatureRequirement> requirements,
        IEnumerable<SignatureResolution> resolutions)
    {
        var byId = resolutions.ToDictionary(x => x.Id, StringComparer.Ordinal);

        foreach (var requirement in requirements)
        {
            if (!byId.TryGetValue(requirement.Id, out var resolution))
                return SignatureGateResult.Blocked(requirement.Id, "resolution missing");

            if (resolution.MatchCount == 0)
                return SignatureGateResult.Blocked(requirement.Id, "zero matches");

            if (requirement.MustBeUnique && resolution.MatchCount != 1)
                return SignatureGateResult.Blocked(requirement.Id, "multiple matches");
        }

        return SignatureGateResult.Allowed();
    }
}
