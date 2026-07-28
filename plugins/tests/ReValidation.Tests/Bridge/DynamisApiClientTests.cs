using System.Numerics;
using ReValidation.DynamisBridge.Ipc;
using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class DynamisApiClientTests
{
    [Fact]
    public void Refresh_WhenGatewayHasSupportedApiVersion_PublishesReady()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: (1, 7, 0));
        using var client = new DynamisApiClient(gateway, requiredMajorVersion: 1, minimumMinorVersion: 7);

        client.Refresh();

        Assert.Equal(BridgeAvailabilityStatus.Ready, client.Current.Status);
        Assert.Equal((uint)1, client.Current.ApiVersion?.MajorVersion);
        Assert.Equal((uint)7, client.Current.ApiVersion?.MinorVersion);
        Assert.Equal((ulong)0, client.Current.ApiVersion?.FeatureFlags);
    }

    [Fact]
    public void Refresh_WhenGatewayHasNoApiVersion_PublishesUnavailable()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: null);
        using var client = new DynamisApiClient(gateway, requiredMajorVersion: 1, minimumMinorVersion: 7);

        client.Refresh();

        Assert.Equal(BridgeAvailabilityStatus.Unavailable, client.Current.Status);
    }

    [Fact]
    public void ApiInitialized_WhenGatewayBecomesAvailable_PublishesReady()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: null);
        using var client = new DynamisApiClient(gateway, requiredMajorVersion: 1, minimumMinorVersion: 7);

        gateway.RaiseApiInitialized(1, 7, 0x20, new Version(2, 3, 4));

        Assert.Equal(BridgeAvailabilityStatus.Ready, client.Current.Status);
        Assert.Equal((ulong)0x20, client.Current.ApiVersion?.FeatureFlags);
    }

    [Fact]
    public void ApiDisposing_WhenGatewayStillReportsSupportedVersion_PublishesUnavailable()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: (1, 7, 0));
        using var client = new DynamisApiClient(gateway, requiredMajorVersion: 1, minimumMinorVersion: 7);
        client.Refresh();

        gateway.RaiseApiDisposing();

        Assert.Equal(BridgeAvailabilityStatus.Unavailable, client.Current.Status);
        Assert.Null(client.Current.ApiVersion);
        Assert.Equal("Dynamis is unavailable.", client.Current.StatusText);
    }

    [Fact]
    public void GatewayMethods_MatchPublishedDynamisAbi()
    {
        AssertMethod(nameof(IDynamisIpcGateway.TryGetApiVersion), typeof((uint, uint, ulong)?));
        AssertMethod(nameof(IDynamisIpcGateway.SubscribeApiInitialized), typeof(IDisposable), typeof(Action<uint, uint, ulong, Version>));
        AssertMethod(nameof(IDynamisIpcGateway.SubscribeApiDisposing), typeof(IDisposable), typeof(Action));
        AssertMethod(nameof(IDynamisIpcGateway.TryInspectObject), typeof(bool), typeof(nint));
        AssertMethod(nameof(IDynamisIpcGateway.TryInspectObject), typeof(bool), typeof(nint), typeof(string));
        AssertMethod(nameof(IDynamisIpcGateway.TryInspectObject), typeof(bool), typeof(nint), typeof(object), typeof(string));
        AssertMethod(nameof(IDynamisIpcGateway.TryInspectRegion), typeof(bool), typeof(nint), typeof(uint), typeof(string), typeof(uint), typeof(uint), typeof(string));
        AssertMethod(nameof(IDynamisIpcGateway.TryGetClass), typeof((string, Type, uint, uint)?), typeof(nint));
        AssertMethod(nameof(IDynamisIpcGateway.TryIsInstanceOf), typeof((bool, uint)?), typeof(nint), typeof(string), typeof(Type));
        AssertMethod(nameof(IDynamisIpcGateway.TryDrawPointer), typeof(bool), typeof(nint), typeof(Func<object>), typeof(Func<string>), typeof(string), typeof(ulong), typeof(Vector2));
    }

    private static void AssertMethod(string name, Type returnType, params Type[] parameterTypes)
    {
        var method = typeof(IDynamisIpcGateway).GetMethod(name, parameterTypes);

        Assert.NotNull(method);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private sealed class FakeDynamisIpcGateway((uint MajorVersion, uint MinorVersion, ulong FeatureFlags)? apiVersion) : IDynamisIpcGateway
    {
        private Action<uint, uint, ulong, Version>? apiInitialized;
        private Action? apiDisposing;

        public (uint MajorVersion, uint MinorVersion, ulong FeatureFlags)? ApiVersion { get; set; } = apiVersion;

        public (uint MajorVersion, uint MinorVersion, ulong FeatureFlags)? TryGetApiVersion() => ApiVersion;
        public IDisposable SubscribeApiInitialized(Action<uint, uint, ulong, Version> handler)
        {
            apiInitialized += handler;
            return new Subscription(() => apiInitialized -= handler);
        }

        public IDisposable SubscribeApiDisposing(Action handler)
        {
            apiDisposing += handler;
            return new Subscription(() => apiDisposing -= handler);
        }

        public void RaiseApiInitialized(uint majorVersion, uint minorVersion, ulong featureFlags, Version pluginVersion) =>
            apiInitialized?.Invoke(majorVersion, minorVersion, featureFlags, pluginVersion);

        public void RaiseApiDisposing() => apiDisposing?.Invoke();
        public bool TryInspectObject(nint address) => true;
        public bool TryInspectObject(nint address, string? name) => true;
        public bool TryInspectObject(nint address, object? @class, string? name) => true;
        public bool TryInspectRegion(nint address, uint size, string typeName, uint typeTemplateId, uint classKindId, string? name) => true;
        public (string Name, Type? Type, uint Size, uint Displacement)? TryGetClass(nint pointer) => null;
        public (bool IsInstance, uint Displacement)? TryIsInstanceOf(nint pointer, string? className, Type? type) => null;
        public bool TryDrawPointer(nint pointer, Func<object?>? @class, Func<string?>? name, string? customText, ulong flags, Vector2 size) => true;
        public void Dispose() { }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
