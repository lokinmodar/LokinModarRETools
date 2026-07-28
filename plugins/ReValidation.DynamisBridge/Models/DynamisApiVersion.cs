namespace ReValidation.DynamisBridge.Models;

public readonly record struct DynamisApiVersion(
    uint MajorVersion,
    uint MinorVersion,
    ulong FeatureFlags)
{
    public override string ToString() => $"{MajorVersion}.{MinorVersion}";
}
