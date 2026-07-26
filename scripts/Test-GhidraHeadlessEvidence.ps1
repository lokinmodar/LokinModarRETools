[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$adapter = Join-Path $PSScriptRoot 'Invoke-GhidraFunctionEvidence.ps1'
$projectDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('ffxiv-client-structs-ghidra-test-' + [guid]::NewGuid().ToString('N'))
$outputPath = Join-Path $projectDirectory 'evidence.md'

$preview = & $adapter `
    -Executable $Executable `
    -Rva '0x12D8850' `
    -ProjectDirectory $projectDirectory `
    -ProjectName 'TestProject' `
    -OutputPath $outputPath `
    -AsCommand

if ($preview.Mode -ne 'Import') {
    throw "Expected Import mode for a new project, got '$($preview.Mode)'."
}
if ($preview.Rvas.Count -ne 1 -or $preview.Rvas[0] -ne '0x12D8850') {
    throw 'Expected the preview to normalize and retain the requested RVA.'
}
if ($preview.Arguments -notcontains '-scriptPath' -or $preview.Arguments -notcontains '-postScript' -or
    $preview.Arguments -notcontains 'ExportFunctionEvidence.java') {
    throw 'Expected the preview to invoke the local Ghidra post-script.'
}
if ($preview.Arguments -notcontains '-import' -or $preview.Arguments -contains '-process') {
    throw 'Expected a new project preview to import the executable rather than process a cached program.'
}
$importIndex = [array]::IndexOf([string[]]$preview.Arguments, '-import')
$ghidraImportPath = $preview.Arguments[$importIndex + 1]
if ($ghidraImportPath -match '[()]' -or $ghidraImportPath -eq (Resolve-Path -LiteralPath $Executable).Path) {
    throw "Expected the Ghidra import path to avoid cmd.exe-special parentheses, got '$ghidraImportPath'."
}
if (Test-Path -LiteralPath $projectDirectory) {
    throw 'The -AsCommand preview must not create a Ghidra project or output directory.'
}

$cachedProjectDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('ffxiv-client-structs-ghidra-cached-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $cachedProjectDirectory | Out-Null
New-Item -ItemType File -Path (Join-Path $cachedProjectDirectory 'TestProject.gpr') | Out-Null
try {
    $cachedPreview = & $adapter `
        -Executable $Executable `
        -Rva '0x12D8850' `
        -ProjectDirectory $cachedProjectDirectory `
        -ProjectName 'TestProject' `
        -OutputPath (Join-Path $cachedProjectDirectory 'cached-evidence.md') `
        -AsCommand

    if ($cachedPreview.Mode -ne 'Process' -or $cachedPreview.Arguments -notcontains '-process' -or
        $cachedPreview.Arguments -notcontains '-noanalysis' -or $cachedPreview.Arguments -contains '-import') {
        throw 'Expected an existing project preview to process the cached program without reanalysis.'
    }
}
finally {
    Remove-Item -LiteralPath $cachedProjectDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

$existingEvidence = [System.IO.Path]::GetTempFileName()
try {
    $existingEvidenceError = $null
    try {
        & $adapter `
            -Executable $Executable `
            -Rva '0x12D8850' `
            -ProjectDirectory $projectDirectory `
            -ProjectName 'TestProject' `
            -OutputPath $existingEvidence
    }
    catch {
        $existingEvidenceError = $_
    }

    if ($null -eq $existingEvidenceError -or $existingEvidenceError.Exception.Message -notmatch 'already exists') {
        $message = if ($null -eq $existingEvidenceError) { '<no error>' } else { $existingEvidenceError.Exception.Message }
        throw "Expected an existing evidence file to be rejected before Ghidra runs, got '$message'."
    }
}
finally {
    Remove-Item -LiteralPath $existingEvidence -Force -ErrorAction SilentlyContinue
}

Write-Host 'PASS: Ghidra headless evidence command preview checks completed.'
