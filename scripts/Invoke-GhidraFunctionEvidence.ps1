[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable,

    [Parameter(Mandatory)]
    [string[]]$Rva,

    [string]$ProjectDirectory = (Join-Path $env:LOCALAPPDATA 'FFXIVClientStructs\Ghidra'),

    [ValidatePattern('^[^\\/:*?"<>|]+$')]
    [string]$ProjectName = 'ffxiv_dx11',

    [string]$ProgramName,

    [Parameter(Mandatory)]
    [string]$OutputPath,

    [string]$GhidraHome = 'C:\Tools\Ghidra\ghidra_12.1.2_PUBLIC',

    [ValidateRange(1, 86400)]
    [int]$AnalysisTimeoutSeconds = 3600,

    [ValidateRange(1, 64)]
    [int]$MaxCpu = 2,

    [switch]$Reimport,

    [switch]$NoAnalysis,

    [switch]$OverwriteEvidence,

    [switch]$AsCommand
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-NormalizedRva {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Value)

    $trimmed = $Value.Trim()
    if ($trimmed -match '^0x(?<hex>[0-9A-Fa-f]{1,8})$') {
        $parsed = [Convert]::ToUInt64($matches.hex, 16)
    }
    elseif ($trimmed -match '^[0-9]+$') {
        $parsed = [Convert]::ToUInt64($trimmed, 10)
    }
    else {
        throw "Invalid RVA '$Value'. Use 0x-prefixed hexadecimal or decimal notation."
    }

    if ($parsed -gt [uint32]::MaxValue) {
        throw "RVA '$Value' does not fit in a PE RVA."
    }

    '0x{0:X}' -f $parsed
}

function Get-GhidraBatchImportPath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    try {
        $fileSystem = New-Object -ComObject Scripting.FileSystemObject
        $shortPath = [string]$fileSystem.GetFile($Path).ShortPath
    }
    catch {
        throw "Could not resolve a cmd.exe-compatible import path for '$Path': $($_.Exception.Message)"
    }

    if ([string]::IsNullOrWhiteSpace($shortPath) -or $shortPath -match '[()]') {
        throw "Ghidra's batch launcher cannot import '$Path' because its path contains cmd.exe-special parentheses and no compatible 8.3 path is available."
    }

    $shortPath
}

$modulePath = Join-Path $PSScriptRoot 'RE.Signatures.psm1'
$postScriptDirectory = Join-Path $PSScriptRoot 'ghidra'
$postScriptName = 'ExportFunctionEvidence.java'
$postScriptPath = Join-Path $postScriptDirectory $postScriptName
$analyzeHeadless = Join-Path $GhidraHome 'support\analyzeHeadless.bat'

if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
    throw "Local signature module '$modulePath' was not found."
}
if (-not (Test-Path -LiteralPath $postScriptPath -PathType Leaf)) {
    throw "Ghidra post-script '$postScriptPath' was not found."
}
if (-not (Test-Path -LiteralPath $analyzeHeadless -PathType Leaf)) {
    throw "Ghidra's headless analyzer was not found at '$analyzeHeadless'."
}

$executablePath = (Resolve-Path -LiteralPath $Executable -ErrorAction Stop).Path
$projectDirectoryPath = [System.IO.Path]::GetFullPath($ProjectDirectory)
$outputPath = [System.IO.Path]::GetFullPath($OutputPath)
$normalizedRvas = @($Rva | ForEach-Object { ConvertTo-NormalizedRva -Value $_ })
if ($normalizedRvas.Count -eq 0) {
    throw 'At least one RVA is required.'
}

if ([string]::IsNullOrWhiteSpace($ProgramName)) {
    $ProgramName = Split-Path -Leaf $executablePath
}
if ($ProgramName.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "ProgramName '$ProgramName' contains an invalid file-name character."
}

Import-Module $modulePath -Force
$identity = Get-GameBinaryIdentity -Executable $executablePath
$projectFile = Join-Path $projectDirectoryPath ($ProjectName + '.gpr')
$mode = if ($Reimport -or -not (Test-Path -LiteralPath $projectFile -PathType Leaf)) { 'Import' } else { 'Process' }
$ghidraImportPath = if ($mode -eq 'Import') { Get-GhidraBatchImportPath -Path $executablePath } else { $null }
$outputDirectory = Split-Path -Parent $outputPath
$logDirectory = Join-Path $outputDirectory 'ghidra-logs'
$outputBaseName = [System.IO.Path]::GetFileNameWithoutExtension($outputPath)
$arguments = [System.Collections.Generic.List[string]]::new()
$arguments.Add($projectDirectoryPath)
$arguments.Add($ProjectName)
$arguments.Add('-scriptPath')
$arguments.Add($postScriptDirectory)
$arguments.Add('-postScript')
$arguments.Add($postScriptName)
$arguments.Add($outputPath)
$arguments.Add($identity.Sha256)
$arguments.Add(($normalizedRvas -join ','))
$arguments.Add('-max-cpu')
$arguments.Add([string]$MaxCpu)
$arguments.Add('-log')
$arguments.Add((Join-Path $logDirectory ($outputBaseName + '.ghidra.log')))
$arguments.Add('-scriptlog')
$arguments.Add((Join-Path $logDirectory ($outputBaseName + '.script.log')))

if ($mode -eq 'Import') {
    $arguments.Add('-import')
    $arguments.Add($ghidraImportPath)
    if ($Reimport) {
        $arguments.Add('-overwrite')
    }
    if ($NoAnalysis) {
        $arguments.Add('-noanalysis')
    }
    else {
        $arguments.Add('-analysisTimeoutPerFile')
        $arguments.Add([string]$AnalysisTimeoutSeconds)
    }
}
else {
    $arguments.Add('-process')
    $arguments.Add($ProgramName)
    $arguments.Add('-noanalysis')
}

$preview = [PSCustomObject]@{
    Executable = $executablePath
    Sha256 = $identity.Sha256
    ProjectDirectory = $projectDirectoryPath
    ProjectName = $ProjectName
    ProgramName = $ProgramName
    Mode = $mode
    Rvas = $normalizedRvas
    GhidraImportPath = $ghidraImportPath
    OutputPath = $outputPath
    AnalyzeHeadless = $analyzeHeadless
    Arguments = $arguments.ToArray()
}

if ($AsCommand) {
    $preview
    return
}

if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw 'OutputPath must include a directory.'
}
if ((Test-Path -LiteralPath $outputPath -PathType Leaf) -and -not $OverwriteEvidence) {
    throw "Evidence output '$outputPath' already exists. Use -OverwriteEvidence to replace it."
}
if (-not (Test-Path -LiteralPath $projectDirectoryPath -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $projectDirectoryPath | Out-Null
}
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}
if (-not (Test-Path -LiteralPath $logDirectory -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
}

& $analyzeHeadless @($arguments.ToArray())
if ($LASTEXITCODE -ne 0) {
    throw "Ghidra headless analysis failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
    throw "Ghidra completed without creating evidence output '$outputPath'."
}

[PSCustomObject]@{
    Status = 'Completed'
    Executable = $executablePath
    Sha256 = $identity.Sha256
    ProjectDirectory = $projectDirectoryPath
    ProjectName = $ProjectName
    ProgramName = $ProgramName
    Mode = $mode
    Rvas = $normalizedRvas
    GhidraImportPath = $ghidraImportPath
    OutputPath = $outputPath
    AnalyzeHeadless = $analyzeHeadless
}
