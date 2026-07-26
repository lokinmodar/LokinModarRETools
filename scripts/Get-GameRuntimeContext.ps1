[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable,

    [int]$ProcessId,

    [string]$Rva,

    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-UnsignedAddress {
    param(
        [Parameter(Mandatory)]
        [string]$Value,

        [Parameter(Mandatory)]
        [string]$Name
    )

    $trimmed = $Value.Trim()
    if ($trimmed.StartsWith('0x', [System.StringComparison]::OrdinalIgnoreCase)) {
        $hex = $trimmed.Substring(2)
        if ([string]::IsNullOrWhiteSpace($hex)) {
            throw "$Name must contain hexadecimal digits after 0x."
        }

        try {
            return [Convert]::ToUInt64($hex, 16)
        } catch {
            throw "$Name '$Value' is not a valid unsigned hexadecimal address."
        }
    }

    try {
        return [UInt64]::Parse($trimmed, [System.Globalization.NumberStyles]::Integer, [System.Globalization.CultureInfo]::InvariantCulture)
    } catch {
        throw "$Name '$Value' is not a valid unsigned address."
    }
}

Import-Module (Join-Path $PSScriptRoot 'RE.Signatures.psm1') -Force

$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
$identity = Get-GameBinaryIdentity -Executable $resolvedExecutable
$processIdResult = $null
$processName = $null
$modulePath = $null
$candidateModuleBase = $null
$requestedRva = $null
$candidateRuntimeAddress = $null
$x64DbgModuleExpression = $null
$x64DbgValidationExpression = $null

if ($PSBoundParameters.ContainsKey('Rva')) {
    $rvaValue = ConvertTo-UnsignedAddress -Value $Rva -Name 'Rva'
    $requestedRva = '0x{0:X}' -f $rvaValue
    $moduleName = [System.IO.Path]::GetFileName($resolvedExecutable)
    $x64DbgModuleExpression = '{0}:${1:X}' -f $moduleName, $rvaValue
    $x64DbgValidationExpression = 'mem.valid({0})' -f $x64DbgModuleExpression
}

if ($PSBoundParameters.ContainsKey('ProcessId')) {
    if ($ProcessId -le 0) {
        throw 'ProcessId must be a positive process identifier.'
    }

    $process = Get-Process -Id $ProcessId
    try {
        $module = $process.MainModule
    } catch {
        throw "Unable to read the main module for PID $ProcessId. Run the script with the same privileges as the target process."
    }

    if ($null -eq $module) {
        throw "PID $ProcessId has no readable main module."
    }

    $runtimeModulePath = [System.IO.Path]::GetFullPath($module.FileName)
    if (-not [string]::Equals($resolvedExecutable, $runtimeModulePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "PID $ProcessId is running '$runtimeModulePath', not the requested executable '$resolvedExecutable'."
    }

    $moduleBaseValue = [UInt64]$module.BaseAddress.ToInt64()
    $processIdResult = $process.Id
    $processName = $process.ProcessName
    $modulePath = $runtimeModulePath
    $candidateModuleBase = '0x{0:X}' -f $moduleBaseValue

    if ($null -ne $requestedRva) {
        $candidateRuntimeAddress = '0x{0:X}' -f ($moduleBaseValue + $rvaValue)
    }
}

$result = [pscustomobject]@{
    Binary         = $identity
    ProcessId      = $processIdResult
    ProcessName    = $processName
    ModulePath     = $modulePath
    CandidateModuleBase = $candidateModuleBase
    RequestedRva   = $requestedRva
    CandidateRuntimeAddress = $candidateRuntimeAddress
    X64DbgModuleExpression = $x64DbgModuleExpression
    X64DbgValidationExpression = $x64DbgValidationExpression
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 6
} else {
    $result
}
