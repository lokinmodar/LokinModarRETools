using ReValidation.DynamisBridge.Ipc;
using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class DynamisApiClientTests
{
    [Fact]
    public void Refresh_WhenGatewayHasSupportedApiVersion_PublishesReady()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: 4);
        using var client = new DynamisApiClient(gateway, minimumApiVersion: 4);

        client.Refresh();

        Assert.Equal(BridgeAvailabilityStatus.Ready, client.Current.Status);
        Assert.Equal(4, client.Current.ApiVersion);
    }

    [Fact]
    public void Refresh_WhenGatewayHasNoApiVersion_PublishesUnavailable()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: null);
        using var client = new DynamisApiClient(gateway, minimumApiVersion: 4);

        client.Refresh();

        Assert.Equal(BridgeAvailabilityStatus.Unavailable, client.Current.Status);
    }

    private sealed class FakeDynamisIpcGateway(int? apiVersion) : IDynamisIpcGateway
    {
        public int? TryGetApiVersion() => apiVersion;
        public IDisposable SubscribeApiInitialized(Action handler) => new NullSubscription();
        public IDisposable SubscribeApiDisposing(Action handler) => new NullSubscription();
        public bool TryInspectObject(nint address) => true;
        public bool TryInspectRegion(nint address, nuint size) => true;
        public string? TryGetClassName(nint address) => null;
        public bool TryIsInstanceOf(nint address, string className) => false;
        public bool TryDrawPointer(string label, nint address) => true;
        public void Dispose() { }
    }

    private sealed class NullSubscription : IDisposable
    {
        public void Dispose() { }
    }
}
