[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$StateDirectory,

    [ValidateRange(1, 30)]
    [int]$ReadyWaitSeconds = 10,

    [switch]$Child
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$stateDirectory = [System.IO.Path]::GetFullPath($StateDirectory)
$readyPath = Join-Path $stateDirectory 'ready.json'
$triggerPath = Join-Path $stateDirectory 'trigger'
$continuedPath = Join-Path $stateDirectory 'continued.json'
$releasePath = Join-Path $stateDirectory 'release'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

if ($Child) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class X64DbgDisposableNative
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentProcessId();
}
'@

    $module = [X64DbgDisposableNative]::GetModuleHandle('kernel32.dll')
    $function = [X64DbgDisposableNative]::GetProcAddress($module, 'GetCurrentProcessId')
    if ($module -eq [IntPtr]::Zero -or $function -eq [IntPtr]::Zero) {
        throw 'Could not resolve kernel32!GetCurrentProcessId in the disposable target.'
    }

    $ready = [ordered]@{
        ProcessId       = $PID
        FunctionAddress = '0x{0:X16}' -f [uint64]$function.ToInt64()
    }
    [System.IO.File]::WriteAllText($readyPath, ($ready | ConvertTo-Json -Compress), $utf8NoBom)

    while (-not (Test-Path -LiteralPath $triggerPath)) {
        [System.Threading.Thread]::Sleep(20)
    }

    $observedProcessId = [X64DbgDisposableNative]::GetCurrentProcessId()
    $continued = [ordered]@{
        ProcessId = $observedProcessId
    }
    [System.IO.File]::WriteAllText($continuedPath, ($continued | ConvertTo-Json -Compress), $utf8NoBom)

    while (-not (Test-Path -LiteralPath $releasePath)) {
        [System.Threading.Thread]::Sleep(20)
    }
    exit 0
}

New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
Remove-Item -LiteralPath $readyPath, $triggerPath, $continuedPath, $releasePath -Force -ErrorAction SilentlyContinue

$pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$targetProcess = Start-Process -FilePath $pwsh -ArgumentList @(
    '-NoLogo',
    '-NoProfile',
    '-File', $PSCommandPath,
    '-StateDirectory', $stateDirectory,
    '-Child'
) -PassThru

$deadline = [datetime]::UtcNow.AddSeconds($ReadyWaitSeconds)
while (-not (Test-Path -LiteralPath $readyPath) -and [datetime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 50
}
if (-not (Test-Path -LiteralPath $readyPath)) {
    throw "The disposable target process $($targetProcess.Id) did not publish a ready marker. It was not terminated."
}

$ready = Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json
if ([int]$ready.ProcessId -ne $targetProcess.Id -or [string]::IsNullOrWhiteSpace($ready.FunctionAddress)) {
    throw "The disposable target published an invalid ready marker for process $($targetProcess.Id)."
}

[pscustomobject]@{
    ProcessId       = $targetProcess.Id
    FunctionAddress = [string]$ready.FunctionAddress
    ReadyPath       = $readyPath
    TriggerPath     = $triggerPath
    ContinuedPath   = $continuedPath
    ReleasePath     = $releasePath
}
