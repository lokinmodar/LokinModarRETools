using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Runtime;

public sealed class DalamudOwnerHookInstaller(IGameInteropProvider gameInteropProvider, ulong searchBase) : IOwnerHookInstaller
{
    private readonly IGameInteropProvider gameInteropProvider = gameInteropProvider ?? throw new ArgumentNullException(nameof(gameInteropProvider));

    public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolution);

        if (target.TargetId != JournalHookTargetIds.JournalProvider)
            throw new NotSupportedException($"Owner hook target '{target.TargetId}' is not supported.");

        if (resolution.Rva is null)
            throw new InvalidOperationException("Journal provider signature resolution did not include an RVA.");

        var callSite = checked((nint)(searchBase + resolution.Rva.Value));
        var targetAddress = ResolveRelativeCallTarget(callSite);
        return new JournalProviderOwnerHook(gameInteropProvider, targetAddress);
    }

    private static nint ResolveRelativeCallTarget(nint callSite)
    {
        if (Marshal.ReadByte(callSite) != 0xE8)
            throw new InvalidOperationException("Journal provider signature did not resolve to a relative call.");

        return callSite + 5 + Marshal.ReadInt32(callSite, 1);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte JournalProviderDelegate(ushort questId);

    private sealed class JournalProviderOwnerHook : IOwnerHook
    {
        private readonly ConcurrentQueue<JsonObject> contexts = new();
        private readonly Hook<JournalProviderDelegate> hook;
        private int hits;

        public JournalProviderOwnerHook(IGameInteropProvider gameInteropProvider, nint targetAddress) =>
            hook = gameInteropProvider.HookFromAddress<JournalProviderDelegate>(targetAddress, Detour);

        public int ObservedHitCount => Volatile.Read(ref hits);

        public IReadOnlyList<JsonObject> DrainObservedContexts()
        {
            var observed = new List<JsonObject>();
            while (contexts.TryDequeue(out var context))
                observed.Add(context);

            return observed;
        }

        public void Enable() => hook.Enable();

        public void Dispose() => hook.Dispose();

        private byte Detour(ushort questId)
        {
            Interlocked.Increment(ref hits);
            contexts.Enqueue(new JsonObject { ["questId"] = questId });
            return hook.Original(questId);
        }
    }
}
