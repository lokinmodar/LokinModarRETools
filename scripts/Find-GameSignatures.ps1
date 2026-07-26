[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable,

    [Parameter(Mandatory)]
    [string[]]$Signature,

    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'RE.Signatures.psm1') -Force
$results = @(
    foreach ($pattern in $Signature) {
        Find-GameSignature -Executable $Executable -Signature $pattern
    }
)

if ($AsJson) {
    $results | ConvertTo-Json -Depth 5
} else {
    $results
}
