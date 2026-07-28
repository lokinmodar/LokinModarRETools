using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class DynamisAvailabilityService(IDynamisApiClient apiClient) : IDynamisAvailabilityService
{
    public event Action? AvailabilityChanged
    {
        add => apiClient.AvailabilityChanged += value;
        remove => apiClient.AvailabilityChanged -= value;
    }

    public DynamisAvailabilitySnapshot Current => apiClient.Current;

    public bool IsReady =>
        apiClient.Current.Status is BridgeAvailabilityStatus.Ready or BridgeAvailabilityStatus.SessionActive;
}
