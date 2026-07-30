using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Runtime.HookTargets;

public sealed class JournalProviderOwnerHookBinding(IGameInteropProvider gameInteropProvider) : IOwnerHookBinding
{
    private readonly IGameInteropProvider gameInteropProvider = gameInteropProvider ?? throw new ArgumentNullException(nameof(gameInteropProvider));

    public IOwnerHook Install(nint targetAddress) => new JournalProviderOwnerHook(gameInteropProvider, targetAddress);

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

public sealed class TooltipOwnerHookBinding(
    IGameInteropProvider gameInteropProvider,
    string detailKind) : IOwnerHookBinding
{
    private readonly IGameInteropProvider gameInteropProvider = gameInteropProvider ?? throw new ArgumentNullException(nameof(gameInteropProvider));
    private readonly string detailKind = detailKind is "item" or "action"
        ? detailKind
        : throw new ArgumentOutOfRangeException(nameof(detailKind));

    public IOwnerHook Install(nint targetAddress) => new TooltipOwnerHook(gameInteropProvider, targetAddress, detailKind);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GenerateTooltipDelegate(nint addon, nint numberArray, nint stringArray);

    private sealed class TooltipOwnerHook : IOwnerHook
    {
        private readonly ConcurrentQueue<JsonObject> contexts = new();
        private readonly string detailKind;
        private readonly Hook<GenerateTooltipDelegate> hook;
        private int hits;

        public TooltipOwnerHook(IGameInteropProvider gameInteropProvider, nint targetAddress, string detailKind)
        {
            this.detailKind = detailKind;
            hook = gameInteropProvider.HookFromAddress<GenerateTooltipDelegate>(targetAddress, Detour);
        }

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

        private void Detour(nint addon, nint numberArray, nint stringArray)
        {
            Interlocked.Increment(ref hits);
            contexts.Enqueue(new JsonObject { ["detailKind"] = detailKind });
            hook.Original(addon, numberArray, stringArray);
        }
    }
}
