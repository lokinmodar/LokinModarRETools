namespace ReValidation.Common.Discovery;

public enum DiscoveryMode
{
    Diff,
}

public enum TargetFamily
{
    Journal,
    ItemTooltip,
    ActionTooltip,
}

public enum CueFamily
{
    JournalCompletedList,
    TooltipItemDetail,
    TooltipActionDetail,
}

public enum BindingKind
{
    MemberFunction,
    StaticAddress,
}

public enum ProofProfile
{
    DetourFunction,
    StaticAddressConsumer,
    ConsumerChain,
}
