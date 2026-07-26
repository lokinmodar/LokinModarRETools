[CmdletBinding()]
param(
    [ValidateRange(1, 30)]
    [int]$WaitSeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$launcher = Join-Path $PSScriptRoot 'Start-X64DbgDisposableTarget.ps1'
if (-not (Test-Path -LiteralPath $launcher)) {
    throw 'Start-X64DbgDisposableTarget.ps1 is missing.'
}

$stateDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("ffxiv-x64dbg-disposable-target-test-{0}" -f [guid]::NewGuid().ToString('N'))
$context = $null

try {
    $context = & $launcher -StateDirectory $stateDirectory -ReadyWaitSeconds $WaitSeconds
    foreach ($property in 'ProcessId', 'FunctionAddress', 'ReadyPath', 'TriggerPath', 'ContinuedPath', 'ReleasePath') {
        if ($null -eq $context.$property -or [string]::IsNullOrWhiteSpace([string]$context.$property)) {
            throw "The disposable target context is missing $property."
        }
    }

    if (-not (Test-Path -LiteralPath $context.ReadyPath)) {
        throw 'The disposable target did not write its ready marker.'
    }

    New-Item -ItemType File -Path $context.TriggerPath -Force | Out-Null
    $deadline = [datetime]::UtcNow.AddSeconds($WaitSeconds)
    while (-not (Test-Path -LiteralPath $context.ContinuedPath) -and [datetime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 50
    }
    if (-not (Test-Path -LiteralPath $context.ContinuedPath)) {
        throw 'The disposable target did not reach its continuation marker.'
    }

    $continued = Get-Content -LiteralPath $context.ContinuedPath -Raw | ConvertFrom-Json
    if ([int]$continued.ProcessId -ne [int]$context.ProcessId) {
        throw 'The continuation marker did not identify the launched target process.'
    }

    New-Item -ItemType File -Path $context.ReleasePath -Force | Out-Null
    if (Get-Process -Id $context.ProcessId -ErrorAction SilentlyContinue) {
        Wait-Process -Id $context.ProcessId -Timeout $WaitSeconds
    }
    if (Get-Process -Id $context.ProcessId -ErrorAction SilentlyContinue) {
        throw 'The disposable target did not exit after its continuation marker.'
    }

    Write-Host 'PASS: x64dbg disposable target completed without a debugger.'
} finally {
    if ($null -ne $context -and -not (Test-Path -LiteralPath $context.ContinuedPath)) {
        New-Item -ItemType File -Path $context.TriggerPath -Force | Out-Null
        Wait-Process -Id $context.ProcessId -Timeout $WaitSeconds -ErrorAction SilentlyContinue
    }
    if ($null -ne $context -and -not (Test-Path -LiteralPath $context.ReleasePath)) {
        New-Item -ItemType File -Path $context.ReleasePath -Force | Out-Null
    }

    $fullStateDirectory = [System.IO.Path]::GetFullPath($stateDirectory)
    $fullTempDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($fullStateDirectory.StartsWith($fullTempDirectory, [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Path]::GetFileName($fullStateDirectory).StartsWith('ffxiv-x64dbg-disposable-target-test-', [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $fullStateDirectory)) {
        Remove-Item -LiteralPath $fullStateDirectory -Recurse -Force
    }
}
