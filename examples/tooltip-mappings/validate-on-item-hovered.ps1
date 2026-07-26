[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ClientStructsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ClientStructsRoot = (Resolve-Path -LiteralPath $ClientStructsRoot).Path
$sourcePath = Join-Path $ClientStructsRoot 'FFXIVClientStructs\FFXIV\Client\UI\Agent\AgentItemDetail.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw

$field = '[FieldOffset(0x218)] private byte Unk218;'
$signature = '40 55 53 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 F9 48 81 EC A8 00 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 F7 48 8B 5D 7F 4C 8B F1 8B 4D 77 49 8B F9 44 8B 6D 6F 49 8B F0 4C 89 4D AF 4C 8B FA'
$attribute = "[MemberFunction(`"$signature`")]"
$declaration = 'public partial bool OnItemHovered(InventoryItem** item, InventoryType* inventoryType, ushort* slot, uint index, uint typeOrId, InventoryItem* fallbackItem);'

if (-not $source.Contains($field) -or -not $source.Contains($attribute) -or -not $source.Contains($declaration)) {
    throw 'Missing the ABI-confirmed AgentItemDetail layout or OnItemHovered declaration.'
}
