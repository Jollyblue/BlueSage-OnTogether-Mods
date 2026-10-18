[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$verifier = Join-Path $PSScriptRoot 'Verify-BlueSagePatchedPlayerLimitLift.ps1'
$distDir = Join-Path $projectDir 'dist'

function Get-DistSnapshot {
    if (-not (Test-Path -LiteralPath $distDir)) {
        return @()
    }

    @(
        Get-ChildItem -LiteralPath $distDir -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                [pscustomobject]@{
                    Path = [System.IO.Path]::GetRelativePath($distDir, $_.FullName).Replace('\', '/')
                    Length = $_.Length
                    Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                }
            }
    )
}

$before = @(Get-DistSnapshot)
$output = @(& $verifier -ProjectDir $projectDir -Configuration Release -SkipPackage)
$summary = @($output | Where-Object { $_.PSObject.Properties.Name -contains 'PackageMode' } | Select-Object -Last 1)
$after = @(Get-DistSnapshot)

if ($summary.Count -ne 1) {
    throw 'Skip-package verifier must return exactly one structured summary.'
}
if ([string]$summary[0].PackageMode -cne 'Skipped') {
    throw "Skip-package verifier must report PackageMode=Skipped; found '$($summary[0].PackageMode)'."
}
if ($null -ne $summary[0].Package -or $null -ne $summary[0].PackageSHA256) {
    throw 'Skip-package verifier must not report or hash a package that it did not verify.'
}

$beforeJson = $before | ConvertTo-Json -Depth 4 -Compress
$afterJson = $after | ConvertTo-Json -Depth 4 -Compress
if ($beforeJson -cne $afterJson) {
    throw 'Skip-package verifier changed the dist tree.'
}

Write-Host 'PASS: PlayerLimitLift verifier performed build/static proof without creating, reading, hashing, or mutating dist package bytes.'
