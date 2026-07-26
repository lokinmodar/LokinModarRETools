[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable,

    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'RE.Signatures.psm1') -Force
$identity = Get-GameBinaryIdentity -Executable $Executable

if ($AsJson) {
    $identity | ConvertTo-Json -Depth 5
} else {
    $identity
}
