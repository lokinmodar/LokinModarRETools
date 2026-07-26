[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable,

    [string]$DirectSignature = '48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B 42 28 48 8B F1 48 8B B9 ?? ?? ?? ??',

    [string]$RelativeCallSignature = 'E8 ?? ?? ?? ?? 48 8B 43 28 48 8B AF'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'RE.Signatures.psm1') -Force

$identity = Get-GameBinaryIdentity -Executable $Executable
if ($identity.Machine -ne 'AMD64' -or [string]::IsNullOrWhiteSpace($identity.Sha256) -or $identity.ImageBase -eq 0) {
    throw 'The game binary identity is incomplete.'
}
if (@($identity.Sections | Where-Object IsExecutable).Count -eq 0) {
    throw 'The game binary has no executable PE section.'
}

$direct = @(Find-GameSignature -Executable $Executable -Signature $DirectSignature)
if ($direct.Count -ne 1 -or $direct[0].Status -ne 'Matched' -or $direct[0].MatchCount -ne 1) {
    throw 'Expected exactly one direct signature match.'
}

$missing = @(Find-GameSignature -Executable $Executable -Signature 'DE AD BE EF 13 37')
if ($missing.Count -ne 1 -or $missing[0].Status -ne 'NoMatch' -or $missing[0].MatchCount -ne 0) {
    throw 'Expected the missing signature to be reported as a zero-match result.'
}

$relative = @(Find-GameSignature -Executable $Executable -Signature $RelativeCallSignature)
if ($relative.Count -eq 0 -or @($relative | Where-Object { $_.RelativeTargetRva }).Count -eq 0) {
    throw 'Expected a leading E8 signature with a resolved relative target.'
}

$evidence = @($direct | New-GameSignatureEvidence -NativeOwner 'Client::UI::AddonActionDetail' -Abi 'void(this, NumberArrayData*)' -Result add)
if ($evidence.Count -ne 1 -or $evidence[0] -notmatch 'Client::UI::AddonActionDetail' -or $evidence[0] -notmatch '0x12D8850') {
    throw 'Expected a Markdown evidence row for the direct match.'
}

$identityScript = Join-Path $PSScriptRoot 'Get-GameBinaryIdentity.ps1'
$identityJson = @(& $identityScript -Executable $Executable -AsJson) -join [Environment]::NewLine | ConvertFrom-Json
if ($identityJson.Machine -ne 'AMD64' -or $identityJson.Sha256 -ne $identity.Sha256) {
    throw 'Expected the identity adapter to preserve the identity object in JSON.'
}

$searchScript = Join-Path $PSScriptRoot 'Find-GameSignatures.ps1'
$adapterResults = @(& $searchScript -Executable $Executable -Signature $DirectSignature)
if ($adapterResults.Count -ne 1 -or $adapterResults[0].Rva -ne $direct[0].Rva) {
    throw 'Expected the signature search adapter to preserve match results.'
}

$evidenceScript = Join-Path $PSScriptRoot 'New-GameSignatureEvidence.ps1'
$adapterEvidence = @($direct | & $evidenceScript -NativeOwner 'Client::UI::AddonActionDetail' -Abi 'void(this, NumberArrayData*)' -Result add)
if ($adapterEvidence.Count -ne 1 -or $adapterEvidence[0] -ne $evidence[0]) {
    throw 'Expected the evidence adapter to preserve the Markdown row.'
}

Write-Host 'PASS: local RE signature module checks completed.'
