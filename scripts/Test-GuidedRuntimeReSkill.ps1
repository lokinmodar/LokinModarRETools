[CmdletBinding()]
param(
    [string]$SkillPath = (Join-Path $PSScriptRoot '..\skills\ffxiv-guided-runtime-re')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$skillFile = Join-Path $SkillPath 'SKILL.md'
if (-not (Test-Path -LiteralPath $skillFile)) {
    throw "Missing guided runtime RE skill: $skillFile"
}

$skill = Get-Content -LiteralPath $skillFile -Raw
if ($skill -notmatch '(?ms)^---\s*\r?\nname:\s*ffxiv-guided-runtime-re\s*\r?\ndescription:\s*Use when') {
    throw 'The skill must expose the expected discoverable YAML frontmatter.'
}

$requiredGuidance = @(
    @{ Name = 'explicit per-session authorization'; Pattern = 'explicit user authorization' },
    @{ Name = 'concrete investigation target'; Pattern = 'named function/question' },
    @{ Name = 'single UI cue'; Pattern = 'single user-authorized UI cue' },
    @{ Name = 'no target-memory writes'; Pattern = 'Never inject, patch, write target memory' },
    @{ Name = 'static-to-runtime boundary'; Pattern = 'A runtime capture is evidence, not a C# mapping' },
    @{ Name = 'runtime context tool'; Pattern = 'Get-GameRuntimeContext\.ps1' },
    @{ Name = 'evidence tool'; Pattern = 'New-RuntimeEvidence\.ps1' },
    @{ Name = 'CDB automation prohibition'; Pattern = 'Do not use CDB for live FFXIV breakpoint capture' },
    @{ Name = 'x64dbg supervised condition'; Pattern = 'x64dbg is permitted only in a supervised interactive session' },
    @{ Name = 'x64dbg module validation'; Pattern = 'mem\.valid\(' },
    @{ Name = 'module-relative hardware breakpoint'; Pattern = 'bph ffxiv_dx11\.exe:\$<RVA>,x,1' },
    @{ Name = 'candidate VA prohibition'; Pattern = 'Do not arm a breakpoint from a candidate absolute VA' },
    @{ Name = 'cleanup ordering'; Pattern = 'clear all breakpoints, resume, and only then detach' },
    @{ Name = 'incident code'; Pattern = 'STATUS_SINGLE_STEP' }
)

foreach ($requirement in $requiredGuidance) {
    if ($skill -notmatch $requirement.Pattern) {
        throw "The skill is missing required guidance for $($requirement.Name)."
    }
}

$lineCount = (Get-Content -LiteralPath $skillFile).Count
if ($lineCount -gt 500) {
    throw "The skill has $lineCount lines; keep it below 500 lines."
}

Write-Host 'PASS: guided runtime RE skill checks completed.'
