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

    [Fact]
    public void ApiInitialized_WhenGatewayBecomesAvailable_PublishesReady()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: null);
        using var client = new DynamisApiClient(gateway, minimumApiVersion: 4);

        gateway.ApiVersion = 4;
        gateway.RaiseApiInitialized();

        Assert.Equal(BridgeAvailabilityStatus.Ready, client.Current.Status);
        Assert.Equal(4, client.Current.ApiVersion);
    }

    [Fact]
    public void ApiDisposing_WhenGatewayStillReportsSupportedVersion_PublishesUnavailable()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: 4);
        using var client = new DynamisApiClient(gateway, minimumApiVersion: 4);
        client.Refresh();

        gateway.RaiseApiDisposing();

        Assert.Equal(BridgeAvailabilityStatus.Unavailable, client.Current.Status);
        Assert.Null(client.Current.ApiVersion);
        Assert.Equal("Dynamis is unavailable.", client.Current.StatusText);
    }

    private sealed class FakeDynamisIpcGateway(int? apiVersion) : IDynamisIpcGateway
    {
        private Action? apiInitialized;
        private Action? apiDisposing;

        public int? ApiVersion { get; set; } = apiVersion;

        public int? TryGetApiVersion() => ApiVersion;
        public IDisposable SubscribeApiInitialized(Action handler)
        {
            apiInitialized += handler;
            return new Subscription(() => apiInitialized -= handler);
        }

        public IDisposable SubscribeApiDisposing(Action handler)
        {
            apiDisposing += handler;
            return new Subscription(() => apiDisposing -= handler);
        }

        public void RaiseApiInitialized() => apiInitialized?.Invoke();
        public void RaiseApiDisposing() => apiDisposing?.Invoke();
        public bool TryInspectObject(nint address) => true;
        public bool TryInspectRegion(nint address, nuint size) => true;
        public string? TryGetClassName(nint address) => null;
        public bool TryIsInstanceOf(nint address, string className) => false;
        public bool TryDrawPointer(string label, nint address) => true;
        public void Dispose() { }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
