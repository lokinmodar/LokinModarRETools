namespace ReValidation.Common.Proof;

public interface ITooltipProofHookFactory
{
    ITooltipProofHook Create(string targetId);
}

public interface ITooltipProofHook : IDisposable
{
    int ObservedHitCount { get; }
    void Enable();
}

public sealed class UnavailableTooltipProofHookFactory : ITooltipProofHookFactory
{
    public static UnavailableTooltipProofHookFactory Instance { get; } = new();

    public ITooltipProofHook Create(string targetId) => throw new InvalidOperationException("Tooltip hook installation is not configured.");
}
