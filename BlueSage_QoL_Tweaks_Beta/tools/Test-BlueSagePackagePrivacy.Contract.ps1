[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

$modulePath = Join-Path $PSScriptRoot 'BlueSagePackagePrivacy.psm1'
Import-Module $modulePath -Force

function Get-TestDigest {
    param([string]$Value)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function New-TestZip {
    param(
        [string]$Path,
        [string]$EntryName,
        [byte[]]$Bytes
    )

    $archive = [System.IO.Compression.ZipFile]::Open($Path, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $entry = $archive.CreateEntry($EntryName)
        $stream = $entry.Open()
        try {
            $stream.Write($Bytes, 0, $Bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

$marker = 'Synthetic Private Marker'
$digest = Get-TestDigest $marker
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('bluesage-privacy-contract-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null

try {
    $cleanZip = Join-Path $tempRoot 'clean.zip'
    New-TestZip $cleanZip 'README.md' ([System.Text.Encoding]::UTF8.GetBytes('ordinary public release text'))
    $clean = Test-BlueSageZipPrivacy -ZipPath $cleanZip -ForbiddenDigests @($digest)
    if (-not $clean.Passed -or $clean.FindingCount -ne 0) {
        throw 'Clean package did not pass the privacy scanner.'
    }

    $entryNameZip = Join-Path $tempRoot 'entry-name.zip'
    New-TestZip $entryNameZip "notes/$marker.txt" ([System.Text.Encoding]::UTF8.GetBytes('ordinary content'))
    $entryNameResult = Test-BlueSageZipPrivacy -ZipPath $entryNameZip -ForbiddenDigests @($digest)
    if ($entryNameResult.Passed -or $entryNameResult.FindingCount -lt 1) {
        throw 'Protected digest in a ZIP entry name was not detected.'
    }

    $asciiZip = Join-Path $tempRoot 'ascii.zip'
    New-TestZip $asciiZip 'payload.bin' ([System.Text.Encoding]::ASCII.GetBytes("prefix $marker suffix"))
    $asciiResult = Test-BlueSageZipPrivacy -ZipPath $asciiZip -ForbiddenDigests @($digest)
    if ($asciiResult.Passed -or $asciiResult.FindingCount -lt 1) {
        throw 'Protected digest in printable ASCII payload content was not detected.'
    }

    $utf16Zip = Join-Path $tempRoot 'utf16-offset.zip'
    $utf16 = [System.Text.Encoding]::Unicode.GetBytes("prefix $marker suffix")
    $offsetBytes = [byte[]]::new($utf16.Length + 1)
    $offsetBytes[0] = 0x7f
    [System.Array]::Copy($utf16, 0, $offsetBytes, 1, $utf16.Length)
    New-TestZip $utf16Zip 'payload.bin' $offsetBytes
    $utf16Result = Test-BlueSageZipPrivacy -ZipPath $utf16Zip -ForbiddenDigests @($digest)
    if ($utf16Result.Passed -or $utf16Result.FindingCount -lt 1) {
        throw 'Protected digest in odd-aligned UTF-16LE payload content was not detected.'
    }

    foreach ($result in @($clean, $entryNameResult, $asciiResult, $utf16Result)) {
        if ($result.PSObject.Properties.Name -contains 'MatchedText') {
            throw 'Privacy scanner results must never expose matched content.'
        }
    }

    $boundedZip = Join-Path $tempRoot 'bounded-runtime.zip'
    $boundedBytes = [byte[]]::new(512 * 1024)
    for ($index = 0; $index -lt $boundedBytes.Length; $index++) {
        $boundedBytes[$index] = [byte]($index % 251)
    }
    New-TestZip $boundedZip 'payload.bin' $boundedBytes
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $boundedResult = Test-BlueSageZipPrivacy -ZipPath $boundedZip -ForbiddenDigests @($digest)
    $stopwatch.Stop()
    if (-not $boundedResult.Passed) {
        throw 'Clean bounded-runtime package did not pass the privacy scanner.'
    }
    if ($stopwatch.Elapsed.TotalSeconds -gt 10) {
        throw "Privacy scan exceeded the 10-second contract: elapsedMs=$($stopwatch.ElapsedMilliseconds)"
    }

    $invalidUnicodeZip = Join-Path $tempRoot 'invalid-unicode.zip'
    New-TestZip $invalidUnicodeZip 'payload.bin' ([byte[]](0x00, 0xD8, 0x41, 0x00))
    $invalidUnicodeResult = Test-BlueSageZipPrivacy -ZipPath $invalidUnicodeZip -ForbiddenDigests @($digest)
    if (-not $invalidUnicodeResult.Passed -or $invalidUnicodeResult.ScanErrorCount -ne 0) {
        throw 'Malformed binary Unicode must be scanned fail-safe without producing a scan error.'
    }

    $unpairedSurrogate = [string][char]0xD800
    $surrogateHit = [BlueSage.PackagePrivacyScanner]::ContainsForbiddenDigest($unpairedSurrogate, [string[]]@($digest))
    if ($surrogateHit) {
        throw 'An isolated surrogate must not produce a privacy match.'
    }

    $nonCharacter = [string][char]0xFFFF
    $nonCharacterHit = [BlueSage.PackagePrivacyScanner]::ContainsForbiddenDigest($nonCharacter, [string[]]@($digest))
    if ($nonCharacterHit) {
        throw 'A Unicode noncharacter must not produce a privacy match.'
    }

    $iconZip = Join-Path $tempRoot 'binary-icon.zip'
    New-TestZip $iconZip 'icon.png' ([System.IO.File]::ReadAllBytes((Join-Path $PSScriptRoot '..\icon.png')))
    $iconResult = Test-BlueSageZipPrivacy -ZipPath $iconZip -ForbiddenDigests @($digest)
    if (-not $iconResult.Passed -or $iconResult.ScanErrorCount -ne 0) {
        throw 'Opaque binary release assets must be scanned without producing a scan error.'
    }

    $releaseDllPath = Join-Path $PSScriptRoot '..\bin\Release\net46\BlueSage_QoL_Tweaks_Beta.dll'
    if (-not (Test-Path -LiteralPath $releaseDllPath -PathType Leaf)) {
        throw 'Release DLL fixture is unavailable for the package privacy runtime contract.'
    }
    $dllZip = Join-Path $tempRoot 'release-dll.zip'
    New-TestZip $dllZip 'BepInEx/plugins/BlueSage_QoL_Tweaks_Beta/BlueSage_QoL_Tweaks_Beta.dll' ([System.IO.File]::ReadAllBytes($releaseDllPath))
    $dllStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $dllResult = Test-BlueSageZipPrivacy -ZipPath $dllZip -ForbiddenDigests @($digest)
    $dllStopwatch.Stop()
    if (-not $dllResult.Passed -or $dllResult.ScanErrorCount -ne 0) {
        throw 'Managed release binaries must be scanned without producing a scan error.'
    }
    if ($dllStopwatch.Elapsed.TotalSeconds -gt 10) {
        throw "Managed release binary scan exceeded the 10-second contract: elapsedMs=$($dllStopwatch.ElapsedMilliseconds)"
    }

    Write-Output 'BlueSage package privacy contract: 10/10 passed'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
