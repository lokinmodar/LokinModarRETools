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

begin {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    Import-Module (Join-Path $PSScriptRoot 'RE.Signatures.psm1') -Force
}

process {
    $parameters = @{
        InputObject = $InputObject
        NativeOwner = $NativeOwner
        Abi = $Abi
        Result = $Result
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $parameters.OutputPath = $OutputPath
    }

    New-GameSignatureEvidence @parameters
}
