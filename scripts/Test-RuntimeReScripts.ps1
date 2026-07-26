[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'RE.Signatures.psm1') -Force

$contextScript = Join-Path $PSScriptRoot 'Get-GameRuntimeContext.ps1'
if (-not (Test-Path -LiteralPath $contextScript)) {
    throw 'Get-GameRuntimeContext.ps1 is missing.'
}

$safetyDocumentationPath = Join-Path $PSScriptRoot '..\docs\runtime-safety.md'
if (-not (Test-Path -LiteralPath $safetyDocumentationPath)) {
    throw 'Expected the runtime safety guidance to exist.'
}

$incident = Get-Content -LiteralPath $safetyDocumentationPath -Raw
if ($incident -notmatch '0x80000004' -or $incident -notmatch 'STATUS_SINGLE_STEP') {
    throw 'Expected the safety guidance to retain the reviewed exception code.'
}

$identity = Get-GameBinaryIdentity -Executable $Executable
$staticContext = @(& $contextScript -Executable $Executable -AsJson) -join [Environment]::NewLine | ConvertFrom-Json
if ($staticContext.Binary.Sha256 -ne $identity.Sha256 -or $null -ne $staticContext.ProcessId -or $null -ne $staticContext.CandidateModuleBase -or $null -ne $staticContext.CandidateRuntimeAddress) {
    throw 'Expected static runtime context without inspecting a running process.'
}

$selfProcess = Get-Process -Id $PID
$selfContext = @(& $contextScript -Executable $selfProcess.Path -ProcessId $PID -Rva '0x0' -AsJson) -join [Environment]::NewLine | ConvertFrom-Json
if ($selfContext.PSObject.Properties['ModuleBase'] -or $selfContext.PSObject.Properties['RuntimeAddress']) {
    throw 'Runtime context must not expose ambiguous authoritative address properties.'
}

$selfModuleName = [System.IO.Path]::GetFileName($selfProcess.Path)
$expectedModuleExpression = '{0}:$0' -f $selfModuleName
$expectedValidationExpression = 'mem.valid({0})' -f $expectedModuleExpression
if ($selfContext.ProcessId -ne $PID -or
    [string]::IsNullOrWhiteSpace($selfContext.CandidateModuleBase) -or
    $selfContext.CandidateRuntimeAddress -ne $selfContext.CandidateModuleBase -or
    $selfContext.X64DbgModuleExpression -ne $expectedModuleExpression -or
    $selfContext.X64DbgValidationExpression -ne $expectedValidationExpression) {
    throw 'Expected candidate-only process context and an x64dbg validation expression.'
}

$evidenceScript = Join-Path $PSScriptRoot 'New-RuntimeEvidence.ps1'
if (-not (Test-Path -LiteralPath $evidenceScript)) {
    throw 'New-RuntimeEvidence.ps1 is missing.'
}

$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("ffxiv-runtime-re-test-{0}" -f [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$transcriptPath = Join-Path $testDirectory 'capture.txt'
$reportPath = Join-Path $testDirectory 'evidence.md'
$x64DbgMissingReportPath = Join-Path $testDirectory 'x64dbg-missing-evidence.md'
$x64DbgReportPath = Join-Path $testDirectory 'x64dbg-evidence.md'
Set-Content -LiteralPath $transcriptPath -Encoding utf8NoBOM -Value @(
    'r rcx rdx r8 r9',
    'rcx=0000000000000001 rdx=0000000000000002 r8=0000000000000003 r9=0000000000000004'
)

$x64DbgContext = [pscustomobject]@{
    Binary                    = $identity
    ProcessId                 = 1234
    ProcessName               = 'ffxiv_dx11'
    ModulePath                = $Executable
    CandidateModuleBase       = '0x7FF700000000'
    RequestedRva              = '0x1038DC0'
    CandidateRuntimeAddress   = '0x7FF701038DC0'
    X64DbgModuleExpression   = 'ffxiv_dx11.exe:$1038DC0'
    X64DbgValidationExpression = 'mem.valid(ffxiv_dx11.exe:$1038DC0)'
}

$missingObserved = $false
try {
    & $evidenceScript -Context $x64DbgContext -Question 'Confirm x64dbg evidence validation.' -FunctionRva '0x1038DC0' -TranscriptPath $transcriptPath -OutputPath $x64DbgMissingReportPath -Debugger x64dbg | Out-Null
} catch {
    $missingObserved = $_.Exception.Message -match 'ObservedModuleExpression'
}
if (-not $missingObserved) {
    throw 'Expected x64dbg evidence to require ObservedModuleExpression.'
}

& $evidenceScript -Context $x64DbgContext -Question 'Confirm x64dbg evidence validation.' -FunctionRva '0x1038DC0' -TranscriptPath $transcriptPath -OutputPath $x64DbgReportPath -Debugger x64dbg -ObservedModuleExpression 'ffxiv_dx11.exe:$1038DC0' -ObservedValidationResult 1 -ObservedRuntimeAddress '0x00007FF649028DC0' | Out-Null
$x64DbgReport = Get-Content -LiteralPath $x64DbgReportPath -Raw
foreach ($expectedText in @('Candidate module base', 'Candidate runtime address', 'Observed module expression', 'Observed runtime address', 'ffxiv_dx11.exe:$1038DC0', '0x00007FF649028DC0')) {
    if ($x64DbgReport -notmatch [regex]::Escape($expectedText)) {
        throw "Expected x64dbg report to contain '$expectedText'."
    }
}

& $evidenceScript -Context $staticContext -Question 'Confirm a test-only register capture.' -FunctionRva '0x12D8850' -TranscriptPath $transcriptPath -OutputPath $reportPath -ExpectedField 'RCX', 'R9' | Out-Null
if (-not (Test-Path -LiteralPath $reportPath)) {
    throw 'Expected the runtime evidence report to be created.'
}

$report = Get-Content -LiteralPath $reportPath -Raw
if ($report -notmatch [regex]::Escape($identity.Sha256) -or $report -notmatch '0x12D8850' -or $report -notmatch 'RCX') {
    throw 'Expected a traceable runtime evidence report.'
}

$overwriteBlocked = $false
try {
    & $evidenceScript -Context $staticContext -Question 'Confirm a test-only register capture.' -FunctionRva '0x12D8850' -TranscriptPath $transcriptPath -OutputPath $reportPath
} catch {
    $overwriteBlocked = $_.Exception.Message -match 'already exists'
}
if (-not $overwriteBlocked) {
    throw 'Expected runtime evidence to refuse an overwrite without -Overwrite.'
}

Write-Host 'PASS: runtime RE script checks completed.'
