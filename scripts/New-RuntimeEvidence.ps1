[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [object]$Context,

    [Parameter(Mandatory)]
    [string]$Question,

    [Parameter(Mandatory)]
    [string]$FunctionRva,

    [Parameter(Mandatory)]
    [string]$TranscriptPath,

    [Parameter(Mandatory)]
    [string]$OutputPath,

    [string]$Debugger = 'Unspecified debugger',

    [string]$ObservedModuleExpression,

    [int]$ObservedValidationResult,

    [string]$ObservedRuntimeAddress,

    [string]$Interaction = 'No interaction recorded',

    [string]$Conclusion = 'Pending manual interpretation.',

    [string[]]$ExpectedField = @(),

    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ContextProperty {
    param(
        [Parameter(Mandatory)]
        [object]$InputObject,

        [Parameter(Mandatory)]
        [string]$Name
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

$binary = Get-ContextProperty -InputObject $Context -Name 'Binary'
if ($null -eq $binary) {
    throw 'Context must contain the Binary identity emitted by Get-GameRuntimeContext.ps1.'
}

$sha256 = Get-ContextProperty -InputObject $binary -Name 'Sha256'
if ([string]::IsNullOrWhiteSpace($sha256)) {
    throw 'Context.Binary must contain Sha256.'
}

$isX64Dbg = [string]::Equals($Debugger, 'x64dbg', [System.StringComparison]::OrdinalIgnoreCase)
if ($isX64Dbg) {
    if ([string]::IsNullOrWhiteSpace($ObservedModuleExpression)) {
        throw 'ObservedModuleExpression is required for x64dbg evidence.'
    }

    if ($ObservedValidationResult -ne 1) {
        throw 'ObservedValidationResult must be 1 for x64dbg evidence.'
    }

    if ([string]::IsNullOrWhiteSpace($ObservedRuntimeAddress)) {
        throw 'ObservedRuntimeAddress is required for x64dbg evidence.'
    }
}

$resolvedTranscript = (Resolve-Path -LiteralPath $TranscriptPath).Path
$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
if ((Test-Path -LiteralPath $fullOutputPath) -and -not $Overwrite) {
    throw "Output path already exists: $fullOutputPath. Use -Overwrite only after reviewing the existing evidence."
}

$outputDirectory = Split-Path -Parent $fullOutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$transcript = Get-Content -LiteralPath $resolvedTranscript -Raw
$processId = Get-ContextProperty -InputObject $Context -Name 'ProcessId'
$candidateModuleBase = Get-ContextProperty -InputObject $Context -Name 'CandidateModuleBase'
$candidateRuntimeAddress = Get-ContextProperty -InputObject $Context -Name 'CandidateRuntimeAddress'
$modulePath = Get-ContextProperty -InputObject $Context -Name 'ModulePath'
$expectedFields = if ($ExpectedField.Count -gt 0) { ($ExpectedField | ForEach-Object { '`{0}`' -f $_ }) -join ', ' } else { 'Not specified' }
$processIdDisplay = if ($null -eq $processId) { 'not attached' } else { $processId }
$candidateModuleBaseDisplay = if ($null -eq $candidateModuleBase) { 'not available' } else { $candidateModuleBase }
$candidateRuntimeAddressDisplay = if ($null -eq $candidateRuntimeAddress) { 'not available' } else { $candidateRuntimeAddress }
$observedModuleExpressionDisplay = if ([string]::IsNullOrWhiteSpace($ObservedModuleExpression)) { 'not observed' } else { $ObservedModuleExpression }
$observedValidationResultDisplay = if ($isX64Dbg) { $ObservedValidationResult } else { 'not observed' }
$observedRuntimeAddressDisplay = if ([string]::IsNullOrWhiteSpace($ObservedRuntimeAddress)) { 'not observed' } else { $ObservedRuntimeAddress }
$modulePathDisplay = if ($null -eq $modulePath) { 'not observed' } else { $modulePath }

$markdown = @'
# Runtime RE evidence

> Scope guard: this report records one user-authorized observation. It is not a declaration of an ABI or a ClientStructs mapping.

## Question

{0}

## Target

| Item | Value |
| --- | --- |
| Binary SHA-256 | `{1}` |
| Function RVA | `{2}` |
| PID | `{3}` |
| Candidate module base | `{4}` |
| Candidate runtime address | `{5}` |
| Observed module expression | `{6}` |
| Observed module validation | `{7}` |
| Observed runtime address | `{8}` |
| Module path | `{9}` |
| Debugger | {10} |
| UI interaction | {11} |
| Expected capture fields | {12} |

## Conclusion

{13}

## Debugger transcript

The transcript below must contain only the requested registers, call stack, and selected fields; review it before sharing.

````text
{14}
````
'@ -f $Question, $sha256, $FunctionRva, $processIdDisplay, $candidateModuleBaseDisplay, $candidateRuntimeAddressDisplay, $observedModuleExpressionDisplay, $observedValidationResultDisplay, $observedRuntimeAddressDisplay, $modulePathDisplay, $Debugger, $Interaction, $expectedFields, $Conclusion, $transcript

Set-Content -LiteralPath $fullOutputPath -Value $markdown -Encoding utf8NoBOM
Get-Item -LiteralPath $fullOutputPath
