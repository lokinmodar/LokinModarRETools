Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-GameBytePattern {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Signature)

    $tokens = $Signature.Trim() -split '\s+'
    if ($tokens.Count -eq 0 -or [string]::IsNullOrWhiteSpace($tokens[0])) {
        throw [System.ArgumentException]::new('Signature must contain at least one byte.')
    }

    $pattern = [int[]]::new($tokens.Count)
    $fixedByteCount = 0
    for ($index = 0; $index -lt $tokens.Count; $index++) {
        $token = $tokens[$index]
        if ($token -eq '??') {
            $pattern[$index] = -1
            continue
        }
        if ($token -notmatch '^[0-9A-Fa-f]{2}$') {
            throw [System.ArgumentException]::new("Invalid byte token '$token'. Use two hex characters or ??.")
        }

        $pattern[$index] = [Convert]::ToInt32($token, 16)
        $fixedByteCount++
    }

    if ($fixedByteCount -eq 0) {
        throw [System.ArgumentException]::new('Signature cannot consist entirely of wildcards.')
    }

    $pattern
}

function Get-GamePeImage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Executable)

    $path = (Resolve-Path -LiteralPath $Executable -ErrorAction Stop).Path
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 0x40 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
        throw [System.ArgumentException]::new("'$path' is not a DOS/PE executable.")
    }

    $peOffset = [System.BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -lt 0x40 -or $peOffset + 0x18 -gt $bytes.Length -or
        [System.Text.Encoding]::ASCII.GetString($bytes, $peOffset, 4) -ne "PE$([char]0)$([char]0)") {
        throw [System.ArgumentException]::new("'$path' has an invalid PE header.")
    }

    $machine = [System.BitConverter]::ToUInt16($bytes, $peOffset + 4)
    if ($machine -ne 0x8664) {
        throw [System.ArgumentException]::new("'$path' is not an AMD64 PE image (machine: 0x$('{0:X4}' -f $machine)).")
    }

    $sectionCount = [System.BitConverter]::ToUInt16($bytes, $peOffset + 6)
    $optionalHeaderSize = [System.BitConverter]::ToUInt16($bytes, $peOffset + 20)
    $optionalHeaderOffset = $peOffset + 24
    if ($optionalHeaderSize -lt 32 -or $optionalHeaderOffset + $optionalHeaderSize -gt $bytes.Length -or
        [System.BitConverter]::ToUInt16($bytes, $optionalHeaderOffset) -ne 0x20B) {
        throw [System.ArgumentException]::new("'$path' is not a PE32+ image.")
    }

    $sectionHeaderOffset = $optionalHeaderOffset + $optionalHeaderSize
    if ($sectionHeaderOffset + ($sectionCount * 40) -gt $bytes.Length) {
        throw [System.ArgumentException]::new("'$path' has section headers outside the file.")
    }

    $sections = [System.Collections.Generic.List[object]]::new()
    for ($sectionIndex = 0; $sectionIndex -lt $sectionCount; $sectionIndex++) {
        $offset = $sectionHeaderOffset + ($sectionIndex * 40)
        $name = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, 8).Trim([char]0)
        $virtualSize = [System.BitConverter]::ToUInt32($bytes, $offset + 8)
        $rva = [System.BitConverter]::ToUInt32($bytes, $offset + 12)
        $rawSize = [System.BitConverter]::ToUInt32($bytes, $offset + 16)
        $rawOffset = [System.BitConverter]::ToUInt32($bytes, $offset + 20)
        $characteristics = [System.BitConverter]::ToUInt32($bytes, $offset + 36)
        if ([uint64]$rawOffset + [uint64]$rawSize -gt [uint64]$bytes.Length) {
            throw [System.ArgumentException]::new("'$path' section '$name' has raw data outside the file.")
        }

        $sections.Add([PSCustomObject]@{
            Name = $name
            Rva = $rva
            VirtualSize = $virtualSize
            RawOffset = $rawOffset
            RawSize = $rawSize
            IsExecutable = (($characteristics -band [uint32]0x20000000) -ne 0)
        })
    }

    [PSCustomObject]@{
        Path = $path
        Bytes = $bytes
        Machine = $machine
        ImageBase = [System.BitConverter]::ToUInt64($bytes, $optionalHeaderOffset + 24)
        Sections = $sections.ToArray()
    }
}

function Get-GameSectionForRva {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Sections,
        [Parameter(Mandatory)][uint32]$Rva
    )

    foreach ($section in $Sections) {
        $size = [Math]::Max([uint64]$section.VirtualSize, [uint64]$section.RawSize)
        if ([uint64]$Rva -ge [uint64]$section.Rva -and [uint64]$Rva -lt [uint64]$section.Rva + $size) {
            return $section.Name
        }
    }

    $null
}

function Get-GameBinaryIdentity {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Executable)

    $image = Get-GamePeImage -Executable $Executable
    $file = Get-Item -LiteralPath $image.Path
    $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($image.Path)

    [PSCustomObject]@{
        Executable = $image.Path
        Length = [int64]$file.Length
        LastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('O')
        Sha256 = (Get-FileHash -LiteralPath $image.Path -Algorithm SHA256).Hash
        FileVersion = $version.FileVersion
        ProductVersion = $version.ProductVersion
        Machine = 'AMD64'
        ImageBase = ('0x{0:X}' -f $image.ImageBase)
        Sections = @($image.Sections | ForEach-Object {
            [PSCustomObject]@{
                Name = $_.Name
                Rva = ('0x{0:X}' -f $_.Rva)
                VirtualSize = ('0x{0:X}' -f $_.VirtualSize)
                RawOffset = ('0x{0:X}' -f $_.RawOffset)
                RawSize = ('0x{0:X}' -f $_.RawSize)
                IsExecutable = $_.IsExecutable
            }
        })
    }
}

function Find-GameSignature {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string]$Signature
    )

    $pattern = ConvertTo-GameBytePattern -Signature $Signature
    $image = Get-GamePeImage -Executable $Executable
    $sha256 = (Get-FileHash -LiteralPath $image.Path -Algorithm SHA256).Hash
    $anchorIndex = [Array]::FindIndex($pattern, [Predicate[int]]{ param($value) $value -ge 0 })
    $anchorByte = [byte]$pattern[$anchorIndex]
    $normalizedSignature = ($Signature.Trim() -split '\s+' | ForEach-Object { $_.ToUpperInvariant() }) -join ' '
    $matches = [System.Collections.Generic.List[object]]::new()

    foreach ($section in $image.Sections | Where-Object IsExecutable) {
        if ($section.RawSize -lt $pattern.Length) {
            continue
        }

        $firstCandidate = [int]$section.RawOffset + $anchorIndex
        $lastCandidate = [int]$section.RawOffset + [int]$section.RawSize - $pattern.Length + $anchorIndex
        $searchOffset = $firstCandidate
        while ($searchOffset -le $lastCandidate) {
            $anchorOffset = [Array]::IndexOf([byte[]]$image.Bytes, $anchorByte, $searchOffset)
            if ($anchorOffset -lt 0 -or $anchorOffset -gt $lastCandidate) {
                break
            }

            $matchOffset = $anchorOffset - $anchorIndex
            $isMatch = $true
            for ($patternIndex = 0; $patternIndex -lt $pattern.Length; $patternIndex++) {
                if ($pattern[$patternIndex] -ge 0 -and $image.Bytes[$matchOffset + $patternIndex] -ne [byte]$pattern[$patternIndex]) {
                    $isMatch = $false
                    break
                }
            }

            if ($isMatch) {
                $rva = [uint32]([uint64]$section.Rva + [uint64]($matchOffset - [int]$section.RawOffset))
                $targetRva = $null
                $targetSection = $null
                if (($image.Bytes[$matchOffset] -eq 0xE8 -or $image.Bytes[$matchOffset] -eq 0xE9) -and $pattern.Length -ge 5) {
                    $displacement = [System.BitConverter]::ToInt32($image.Bytes, $matchOffset + 1)
                    $target = [int64]$rva + 5 + [int64]$displacement
                    if ($target -ge 0 -and $target -le [uint32]::MaxValue) {
                        $targetRva = [uint32]$target
                        $targetSection = Get-GameSectionForRva -Sections $image.Sections -Rva $targetRva
                    }
                }

                $matches.Add([PSCustomObject]@{
                    Section = $section.Name
                    FileOffset = $matchOffset
                    Rva = $rva
                    TargetRva = $targetRva
                    TargetSection = $targetSection
                })
            }

            $searchOffset = $anchorOffset + 1
        }
    }

    if ($matches.Count -eq 0) {
        [PSCustomObject]@{
            Status = 'NoMatch'
            MatchCount = 0
            MatchIndex = $null
            Executable = $image.Path
            Sha256 = $sha256
            Signature = $normalizedSignature
            Section = $null
            FileOffset = $null
            Rva = $null
            Va = $null
            RelativeTargetRva = $null
            RelativeTargetVa = $null
            RelativeTargetSection = $null
        }
        return
    }

    for ($index = 0; $index -lt $matches.Count; $index++) {
        $match = $matches[$index]
        [PSCustomObject]@{
            Status = 'Matched'
            MatchCount = $matches.Count
            MatchIndex = $index + 1
            Executable = $image.Path
            Sha256 = $sha256
            Signature = $normalizedSignature
            Section = $match.Section
            FileOffset = ('0x{0:X}' -f $match.FileOffset)
            Rva = ('0x{0:X}' -f $match.Rva)
            Va = ('0x{0:X}' -f ([uint64]$image.ImageBase + [uint64]$match.Rva))
            RelativeTargetRva = if ($null -eq $match.TargetRva) { $null } else { '0x{0:X}' -f $match.TargetRva }
            RelativeTargetVa = if ($null -eq $match.TargetRva) { $null } else { '0x{0:X}' -f ([uint64]$image.ImageBase + [uint64]$match.TargetRva) }
            RelativeTargetSection = $match.TargetSection
        }
    }
}

function ConvertTo-MarkdownEvidenceCell {
    [CmdletBinding()]
    param([AllowNull()][object]$Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return 'n/a'
    }

    ([string]$Value).Replace('|', '\|').Replace("`r`n", '<br>').Replace("`n", '<br>')
}

function New-GameSignatureEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [psobject]$InputObject,

        [Parameter(Mandatory)]
        [string]$NativeOwner,

        [Parameter(Mandatory)]
        [string]$Abi,

        [Parameter(Mandatory)]
        [ValidateSet('add', 'update', 'reject')]
        [string]$Result,

        [string]$OutputPath
    )

    process {
        if ($InputObject.Status -ne 'Matched') {
            throw 'Evidence rows require a matched signature result.'
        }

        $row = '| {0} | `{1}` | {2} | {3} | {4} | {5} | {6} |' -f `
            (ConvertTo-MarkdownEvidenceCell $InputObject.Sha256), `
            (ConvertTo-MarkdownEvidenceCell $InputObject.Signature), `
            (ConvertTo-MarkdownEvidenceCell $InputObject.Rva), `
            (ConvertTo-MarkdownEvidenceCell $InputObject.RelativeTargetRva), `
            (ConvertTo-MarkdownEvidenceCell $NativeOwner), `
            (ConvertTo-MarkdownEvidenceCell $Abi), `
            (ConvertTo-MarkdownEvidenceCell $Result)

        if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
            $directory = Split-Path -Parent $OutputPath
            if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory -PathType Container)) {
                throw "Evidence output directory '$directory' does not exist."
            }
            Add-Content -LiteralPath $OutputPath -Value $row -Encoding utf8
        }

        $row
    }
}

Export-ModuleMember -Function Get-GameBinaryIdentity, Find-GameSignature, New-GameSignatureEvidence
