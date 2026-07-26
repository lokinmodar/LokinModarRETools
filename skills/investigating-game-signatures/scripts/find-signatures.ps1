[CmdletBinding()]
param(
    [Alias('Executable')]
    [string]$InputExecutable,
    [Alias('Signature')]
    [string[]]$InputSignature
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-BytePattern {
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

    return $pattern
}

function Get-PeImageInfo {
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
    if ($optionalHeaderOffset + $optionalHeaderSize -gt $bytes.Length -or
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
        $name = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, 8).Trim([char[]]@([char]0))
        $virtualSize = [System.BitConverter]::ToUInt32($bytes, $offset + 8)
        $rva = [System.BitConverter]::ToUInt32($bytes, $offset + 12)
        $rawSize = [System.BitConverter]::ToUInt32($bytes, $offset + 16)
        $rawOffset = [System.BitConverter]::ToUInt32($bytes, $offset + 20)
        $characteristics = [System.BitConverter]::ToUInt32($bytes, $offset + 36)
        if ($rawOffset + $rawSize -gt $bytes.Length) {
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

    return [PSCustomObject]@{
        Path = $path
        Bytes = $bytes
        ImageBase = [System.BitConverter]::ToUInt64($bytes, $optionalHeaderOffset + 24)
        Sections = $sections
    }
}

function Get-SectionForRva {
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

    return $null
}

function Find-GameSignature {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string]$Signature
    )

    $pattern = ConvertTo-BytePattern -Signature $Signature
    $image = Get-PeImageInfo -Executable $Executable
    $anchorIndex = [Array]::FindIndex($pattern, [Predicate[int]]{ param($value) $value -ge 0 })
    $anchorByte = [byte]$pattern[$anchorIndex]
    $normalizedSignature = ($Signature.Trim() -split '\s+' | ForEach-Object { $_.ToUpperInvariant() }) -join ' '

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
                $relativeTargetRva = $null
                $relativeTargetVa = $null
                $relativeTargetSection = $null
                if (($image.Bytes[$matchOffset] -eq 0xE8 -or $image.Bytes[$matchOffset] -eq 0xE9) -and $pattern.Length -ge 5) {
                    $displacement = [System.BitConverter]::ToInt32($image.Bytes, $matchOffset + 1)
                    $target = [int64]$rva + 5 + [int64]$displacement
                    if ($target -ge 0 -and $target -le [uint32]::MaxValue) {
                        $relativeTargetRva = [uint32]$target
                        $relativeTargetVa = [uint64]$image.ImageBase + [uint64]$relativeTargetRva
                        $relativeTargetSection = Get-SectionForRva -Sections $image.Sections.ToArray() -Rva $relativeTargetRva
                    }
                }

                [PSCustomObject]@{
                    Executable = $image.Path
                    Signature = $normalizedSignature
                    Section = $section.Name
                    FileOffset = ('0x{0:X}' -f $matchOffset)
                    Rva = ('0x{0:X}' -f $rva)
                    Va = ('0x{0:X}' -f ([uint64]$image.ImageBase + [uint64]$rva))
                    RelativeTargetRva = if ($null -eq $relativeTargetRva) { $null } else { '0x{0:X}' -f $relativeTargetRva }
                    RelativeTargetVa = if ($null -eq $relativeTargetVa) { $null } else { '0x{0:X}' -f $relativeTargetVa }
                    RelativeTargetSection = $relativeTargetSection
                }
            }

            $searchOffset = $anchorOffset + 1
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($InputExecutable) -or $null -eq $InputSignature -or $InputSignature.Count -eq 0) {
        throw 'Usage: .\find-signatures.ps1 -Executable <path-to-x64-pe> -Signature <"AA BB ??">'
    }

    foreach ($item in $InputSignature) {
        Find-GameSignature -Executable $InputExecutable -Signature $item
    }
}
