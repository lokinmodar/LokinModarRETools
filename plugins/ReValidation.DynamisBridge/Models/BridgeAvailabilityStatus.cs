namespace ReValidation.DynamisBridge.Models;

public enum BridgeAvailabilityStatus
{
    Unavailable,
    Incompatible,
    Ready,
    SessionActive,
}

public sealed record DynamisAvailabilitySnapshot(
    BridgeAvailabilityStatus Status,
    int? ApiVersion,
    string StatusText);
