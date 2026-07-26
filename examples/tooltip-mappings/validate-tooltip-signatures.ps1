[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ClientStructsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ClientStructsRoot = (Resolve-Path -LiteralPath $ClientStructsRoot).Path
$item = Get-Content -Raw (Join-Path $ClientStructsRoot 'FFXIVClientStructs\FFXIV\Client\UI\AddonItemDetail.cs')
$base = Get-Content -Raw (Join-Path $ClientStructsRoot 'FFXIVClientStructs\FFXIV\Client\UI\AddonActionDetailBase.cs')
$action = Get-Content -Raw (Join-Path $ClientStructsRoot 'FFXIVClientStructs\FFXIV\Client\UI\AddonActionDetail.cs')

if ($item -notmatch 'public partial void GenerateTooltip\(NumberArrayData\* numberArray, StringArrayData\* stringArray\);') { throw 'Missing AddonItemDetail.GenerateTooltip.' }
if ($base -notmatch 'Size = 0x248' -or $base -notmatch 'Inherits<AtkUnitBase>, Inherits<AtkManagedInterface>') { throw 'Invalid AddonActionDetailBase layout.' }
if ($action -notmatch 'Size = 0x360' -or $action -notmatch 'Inherits<AddonActionDetailBase>' -or $action -notmatch 'public partial void GenerateTooltip\(NumberArrayData\* numberArray, StringArrayData\* stringArray\);') { throw 'Invalid AddonActionDetail mapping.' }

$updateGroupPositionsSignature = '48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B 42 28 48 8B F1 48 8B B9 ?? ?? ?? ??'
$updateGroupPositionsAttribute = "[MemberFunction(`"$updateGroupPositionsSignature`")]"
$updateGroupPositionsDeclaration = 'public partial void UpdateGroupPositions(NumberArrayData* numberArray);'

if (-not $action.Contains($updateGroupPositionsAttribute) -or -not $action.Contains($updateGroupPositionsDeclaration)) {
    throw 'Missing the ABI-confirmed AddonActionDetail.UpdateGroupPositions declaration.'
}

$agentAction = Get-Content -Raw (Join-Path $ClientStructsRoot 'FFXIVClientStructs\FFXIV\Client\UI\Agent\AgentActionDetail.cs')
if (-not $agentAction.Contains('[FieldOffset(0x4C)] public uint Flags;')) {
    throw 'Missing the ABI-confirmed AgentActionDetail.Flags field.'
}

$idaData = Get-Content -Raw (Join-Path $ClientStructsRoot 'ida\data.yml')
if (-not $idaData.Contains('0x1412D8850: UpdateGroupPositions')) {
    throw 'Missing the AddonActionDetail function rename in ida/data.yml.'
}
