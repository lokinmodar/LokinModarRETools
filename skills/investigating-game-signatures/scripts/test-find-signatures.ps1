param(
    [string]$Executable = 'C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\ffxiv_dx11.exe'
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'find-signatures.ps1')

$relativeCall = Find-GameSignature -Executable $Executable -Signature 'E8 ?? ?? ?? ?? 48 8B 43 28 48 8B AF'
if (@($relativeCall).Count -lt 1) {
    throw 'Expected at least one match for the relative-call pattern.'
}
if (@($relativeCall | Where-Object { $null -ne $_.RelativeTargetRva }).Count -lt 1) {
    throw 'Expected a resolved E8 relative target.'
}

$directFunction = Find-GameSignature -Executable $Executable -Signature '48 89 5C 24 ?? 55 56 57 41 54 41 56 48 83 EC 30 48 8B 9A'
if (@($directFunction).Count -lt 1) {
    throw 'Expected at least one match for the direct-function pattern.'
}
if (@($directFunction | Where-Object { $null -ne $_.RelativeTargetRva }).Count -ne 0) {
    throw 'A pattern not starting with E8/E9 must not have a relative target.'
}

$commandLineResult = @(& (Join-Path $PSScriptRoot 'find-signatures.ps1') -Executable $Executable -Signature 'E8 ?? ?? ?? ?? 48 8B 43 28 48 8B AF')
if ($commandLineResult.Count -lt 1) {
    throw 'Expected the direct script invocation to emit matches.'
}

try {
    Find-GameSignature -Executable $Executable -Signature 'E8 ?'
    throw 'Expected malformed signature validation to fail.'
} catch [System.ArgumentException] {
}

Write-Host 'PASS: signature scanner checks completed.'
