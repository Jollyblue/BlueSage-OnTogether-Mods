[CmdletBinding(DefaultParameterSetName = 'RebuildPackage')]
param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Configuration = 'Release',
    [Parameter(ParameterSetName = 'VerifyExistingPackage')]
    [switch]$VerifyExistingPackage,
    [Parameter(ParameterSetName = 'SkipPackage')]
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'

function Assert-Text {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-ExactInventory {
    param(
        [string[]]$Actual,
        [string[]]$Expected,
        [string]$Label
    )

    $actualSorted = @($Actual | ForEach-Object { $_.Replace('\', '/') } | Sort-Object)
    $expectedSorted = @($Expected | ForEach-Object { $_.Replace('\', '/') } | Sort-Object)
    $differences = @(Compare-Object -ReferenceObject $expectedSorted -DifferenceObject $actualSorted)
    if ($actualSorted.Count -ne $expectedSorted.Count -or $differences.Count -gt 0) {
        $actualList = $actualSorted -join ', '
        $expectedList = $expectedSorted -join ', '
        throw "$Label inventory differs from the exact release contract. Expected: [$expectedList]. Actual: [$actualList]."
    }
}

function Get-StreamSha256 {
    param([System.IO.Stream]$Stream)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha.ComputeHash($Stream)).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

$csproj = Join-Path $ProjectDir 'BlueSagePatched_PlayerLimitLift.csproj'
$pluginSource = Join-Path $ProjectDir 'src\Plugin.cs'
$chatPatchSource = Join-Path $ProjectDir 'src\Patches\TextChannelManagerPatch.cs'
# ilspycmd is pinned in the ModSuite root's dotnet-tools.json (local tool manifest) instead of an
# ad-hoc %TEMP% install: TEMP gets cleaned, and the old install never checked its exit code, so a
# failed install only surfaced later as "ilspycmd.exe is not recognized".
$suiteRoot = (Resolve-Path (Join-Path $ProjectDir '..')).Path
function Invoke-IlSpy {
    param([string[]]$Arguments)
    Push-Location -LiteralPath $suiteRoot
    try { & dotnet tool run ilspycmd -- @Arguments }
    finally { Pop-Location }
}
$dll = Join-Path $ProjectDir "bin\$Configuration\net46\BlueSagePatched_PlayerLimitLift.dll"
$manifestPath = Join-Path $ProjectDir 'manifest.json'
$readmePath = Join-Path $ProjectDir 'README.md'
$changelogPath = Join-Path $ProjectDir 'CHANGELOG.md'
$iconPath = Join-Path $ProjectDir 'icon.png'
$zipPath = Join-Path $ProjectDir 'dist\BlueSagePatched_PlayerLimitLift-1.1.8.zip'
$distDir = Join-Path $ProjectDir 'dist'
$stageDir = Join-Path $distDir 'thunderstore\BlueSagePatched_PlayerLimitLift'
$pluginEntry = 'BepInEx/plugins/BlueSagePatched_PlayerLimitLift/BlueSagePatched_PlayerLimitLift.dll'
$expectedEntries = @(
    $pluginEntry,
    'CHANGELOG.md',
    'icon.png',
    'manifest.json',
    'README.md'
)

if (!(Test-Path -LiteralPath $csproj)) {
    throw "Missing project file: $csproj"
}
if (!(Test-Path -LiteralPath $pluginSource)) {
    throw "Missing plugin source: $pluginSource"
}
if (!(Test-Path -LiteralPath $chatPatchSource)) {
    throw "Missing chat patch source: $chatPatchSource"
}
Push-Location -LiteralPath $suiteRoot
try { dotnet tool restore | Write-Output }
finally { Pop-Location }
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed with exit code $LASTEXITCODE (see $suiteRoot\dotnet-tools.json)"
}
if (!(Test-Path -LiteralPath $manifestPath)) {
    throw "Missing Thunderstore manifest: $manifestPath"
}
if (!(Test-Path -LiteralPath $readmePath)) {
    throw "Missing Thunderstore README: $readmePath"
}
if (!(Test-Path -LiteralPath $changelogPath)) {
    throw "Missing Thunderstore CHANGELOG: $changelogPath"
}
if (!(Test-Path -LiteralPath $iconPath)) {
    throw "Missing Thunderstore icon: $iconPath"
}

$pluginText = Get-Content -LiteralPath $pluginSource -Raw
$chatPatchText = Get-Content -LiteralPath $chatPatchSource -Raw

Assert-Text $pluginText '\[BepInPlugin\(PluginGuid, PluginName, PluginVersion\)\]' 'Plugin must declare BepInPlugin metadata.'
$sourceProcessFilters = @([regex]::Matches($pluginText, '\[BepInProcess\("([^"]+)"\)\]') | ForEach-Object { $_.Groups[1].Value })
if (@($sourceProcessFilters | Where-Object { $_ -eq 'OnTogether.exe' }).Count -ne 1) {
    throw 'Plugin must retain exactly one Windows OnTogether.exe process filter.'
}
if (@($sourceProcessFilters | Where-Object { $_ -eq 'On-Together' }).Count -ne 1) {
    throw 'Plugin must include exactly one extensionless On-Together process filter for macOS/Linux loader eligibility.'
}
if ($sourceProcessFilters.Count -ne 2) {
    throw 'Plugin must not add any process filters beyond OnTogether.exe and On-Together.'
}
Assert-Text $pluginText 'PluginVersion = "1\.1\.8"' 'Plugin version must be 1.1.8.'
Assert-Text $pluginText 'OnDestroy\(\)' 'Plugin must define OnDestroy for Harmony cleanup.'
Assert-Text $pluginText 'UnpatchSelf\(\)' 'Plugin OnDestroy must unpatch its own Harmony patches.'
Assert-Text $pluginText 'MaxLobbySize' 'Plugin must expose MaxLobbySize config.'
Assert-Text $pluginText 'Chainloader\.PluginInfos\.Values' 'Plugin must inspect loaded BepInEx plugin infos for duplicate PlayerLimit diagnostics.'
Assert-Text $pluginText 'Old officerballs PlayerLimitLift appears loaded' 'Plugin must warn when old officerballs PlayerLimitLift appears loaded.'
Assert-Text $pluginText 'Another PlayerLimitLift-like plugin appears loaded' 'Plugin must warn when another PlayerLimit-style mod appears loaded.'
Assert-Text $pluginText 'WarnIfClamped' 'Plugin must warn when config values are clamped to safe bounds.'
Assert-Text $chatPatchText 'nameof\(TextChannelManager\.OnReceivedRpc\)' 'Maintenance release must preserve the field-proven 1.1.1 dispatcher relay.'
Assert-Text $chatPatchText 'RPCInterceptPatcher\(ref int id, ref RPCInfo info, ref RPCPacket packet, ref bool asServer' 'Dispatcher relay prefix must match the current PurrNet signature.'
Assert-Text $chatPatchText 'id != 0' 'Maintenance release must preserve the proven chat RPC guard.'
Assert-Text $chatPatchText 'packet\.data' 'Chat patch must read current PurrNet payloads from RPCPacket.data.'
Assert-Text $chatPatchText 'RPCModule\.PostProcessRpc\(info, ref data\)' 'Chat patch must use current generated-handler post-processing.'
Assert-Text $chatPatchText 'ClampSenderIndexForColor' 'Chat patch must clamp only color index, not sender identity.'
Assert-Text $chatPatchText '__instance\.SendMessageAsync\(textBytes, userName, isLocal, pos, playerID, info\)' 'Above-15 relay must preserve authoritative remote RPCInfo during server re-broadcast.'
if ($chatPatchText -match '__instance\.SendMessageAsync\(textBytes, userName, isLocal, pos, playerID, default\(RPCInfo\)\)') {
    throw 'Above-15 relay still makes remote chat look like a host-local command.'
}

dotnet build $csproj -c $Configuration -warnaserror | Write-Output

if (!(Test-Path -LiteralPath $dll)) {
    throw "Build did not produce expected DLL: $dll"
}

$decompiled = Invoke-IlSpy @('-t', 'BlueSagePatched.PlayerLimitLift.Patches.TextChannelManagerPatch', $dll)
if ($LASTEXITCODE -ne 0) {
    throw "ilspycmd failed with exit code $LASTEXITCODE"
}

$pluginDecompiled = Invoke-IlSpy @('-t', 'BlueSagePatched.PlayerLimitLift.Plugin', $dll)
if ($LASTEXITCODE -ne 0) {
    throw "ilspycmd failed while inspecting plugin metadata with exit code $LASTEXITCODE"
}

Assert-Text $pluginDecompiled '\[BepInPlugin\("com\.bluesage\.ontogether\.playerlimitlift", "BlueSagePatched PlayerLimitLift", "1\.1\.8"\)\]' 'Built DLL plugin identity/version metadata differs from the 1.1.8 release contract.'
$builtProcessFilters = @([regex]::Matches($pluginDecompiled, '\[BepInProcess\("([^"]+)"\)\]') | ForEach-Object { $_.Groups[1].Value })
if (@($builtProcessFilters | Where-Object { $_ -eq 'OnTogether.exe' }).Count -ne 1) {
    throw 'Built DLL must retain exactly one Windows OnTogether.exe process filter.'
}
if (@($builtProcessFilters | Where-Object { $_ -eq 'On-Together' }).Count -ne 1) {
    throw 'Built DLL must include exactly one extensionless On-Together process filter.'
}
if ($builtProcessFilters.Count -ne 2) {
    throw 'Built DLL contains an unapproved process filter.'
}
Assert-Text $decompiled 'RPCInterceptPatcher\(ref int id, ref RPCInfo info, ref RPCPacket packet, ref bool asServer' 'Built DLL does not preserve the proven dispatcher relay signature.'
if ($decompiled -match 'BitPacker stream') {
    throw 'Built DLL still references the removed BitPacker stream prefix parameter.'
}
Assert-Text $decompiled 'OnReceivedRpc' 'Built DLL must preserve the field-proven 1.1.1 dispatcher relay.'
Assert-Text $decompiled 'packet\.data' 'Built DLL must read payload from packet.data.'
Assert-Text $decompiled 'PostProcessRpc\(info, ref data\)' 'Built DLL must post-process packet data before reading.'
Assert-Text $decompiled 'SendMessageAsync\(textBytes, userName, isLocal, pos, playerID, info\)' 'Built DLL must preserve authoritative remote RPCInfo during above-15 server re-broadcast.'

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-Text $manifest.name '^[A-Za-z0-9_]{1,128}$' 'Manifest name must contain only A-Z, a-z, 0-9, and underscore.'
Assert-Text $manifest.version_number '^\d+\.\d+\.\d+$' 'Manifest version_number must be semantic Major.Minor.Patch.'
if ($manifest.description.Length -gt 250) {
    throw 'Manifest description exceeds 250 characters.'
}
if ($manifest.version_number -ne '1.1.8') {
    throw 'Manifest version_number must be 1.1.8.'
}
if (-not ($manifest.dependencies -contains 'BepInEx-BepInExPack-5.4.2305')) {
    throw 'Manifest must depend on current BepInEx-BepInExPack-5.4.2305.'
}

$readmeText = Get-Content -LiteralPath $readmePath -Raw
if ((Get-Item -LiteralPath $readmePath).Length -gt 6000) { throw 'README must remain at or below 6000 bytes.' }
if ((Get-Item -LiteralPath $changelogPath).Length -gt 24000) { throw 'CHANGELOG must remain concise while preserving published-version history.' }
Assert-Text $readmeText 'Version 1\.1\.8 - October 18, 2026' 'README must identify the October 18, 2026 v1.1.8 release.'
Assert-Text $readmeText 'HOST ONLY' 'README must lead with host-only guidance.'
Assert-Text $readmeText 'Quick Start' 'README must provide an immediate-use quick start.'
Assert-Text $readmeText 'ordinary joiners do not install' 'README must clearly tell ordinary joiners not to install PlayerLimitLift.'
Assert-Text $readmeText '128 joinable slots plus the host' 'README must state host-inclusive capacity accurately.'
Assert-Text $readmeText 'BlueSage QoL Tweaks.*`0\.2\.5`' 'README must identify QoL 0.2.5 as the optional companion.'
Assert-Text $readmeText 'another PlayerLimit-style mod' 'README must explain duplicate PlayerLimit-style mod warnings.'
Assert-Text $readmeText 'NickName1' 'README must preserve the old-mod troubleshooting path.'
Assert-Text $readmeText 'macOS and Linux can load the mod, but still need real-world host testing' 'README must keep unproven desktop runtime support explicit in plain language.'
if ($readmeText -match 'pre-publish|runtime promotion|source parity|behavioral and rollback baseline|Published `1\.1\.5`') { throw 'README exposes internal release-history wording.' }

$changelogText = Get-Content -LiteralPath $changelogPath -Raw
Assert-Text $changelogText '(?s)^# Changelog\s+Every previous public release is kept below.*?## 1\.1\.8 — 2026-10-18' 'CHANGELOG must lead with the full-history notice and October 18, 2026 v1.1.8 release.'
Assert-Text $changelogText '## 1\.1\.7 — 2026-07-30' 'CHANGELOG must preserve the July 30, 2026 v1.1.7 release.'
Assert-Text $changelogText 'QoL `0\.2\.4`' 'CHANGELOG must preserve QoL 0.2.4 as the historical optional companion for the 1.1.7 release.'
Assert-Text $changelogText 'host-only' 'CHANGELOG must preserve host-only guidance.'
Assert-Text $changelogText '## 1\.1\.6 — 2026-07-24' 'CHANGELOG must preserve the prior 1.1.6 release.'
Assert-Text $changelogText '## 1\.0\.9 — 2026-06-15' 'CHANGELOG must preserve the first BlueSage PlayerLimitLift release.'
if ($changelogText -match 'pre-publish|runtime promotion|source parity|behavioral and rollback baseline|Published `1\.1\.5`') { throw 'CHANGELOG exposes internal release-history wording.' }

Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($iconPath)
try {
    if ($image.Width -ne 256 -or $image.Height -ne 256) {
        throw "icon.png must be 256x256, got $($image.Width)x$($image.Height)."
    }
}
finally {
    $image.Dispose()
}

if (!$SkipPackage) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (!$VerifyExistingPackage) {
        if (Test-Path -LiteralPath $stageDir) {
            Remove-Item -LiteralPath $stageDir -Recurse -Force
        }
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        New-Item -ItemType Directory -Path (Join-Path $stageDir 'BepInEx\plugins\BlueSagePatched_PlayerLimitLift') -Force | Out-Null
        Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $stageDir 'manifest.json') -Force
        Copy-Item -LiteralPath $readmePath -Destination (Join-Path $stageDir 'README.md') -Force
        Copy-Item -LiteralPath $changelogPath -Destination (Join-Path $stageDir 'CHANGELOG.md') -Force
        Copy-Item -LiteralPath $iconPath -Destination (Join-Path $stageDir 'icon.png') -Force
        Copy-Item -LiteralPath $dll -Destination (Join-Path $stageDir 'BepInEx\plugins\BlueSagePatched_PlayerLimitLift\BlueSagePatched_PlayerLimitLift.dll') -Force

        $stageFiles = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File)
        $stageEntries = @($stageFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($stageDir, $_.FullName).Replace('\', '/') })
        Assert-ExactInventory $stageEntries $expectedEntries 'Thunderstore staging directory'

        $archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            $fixedTimestamp = [DateTimeOffset]::new(2026, 8, 6, 0, 0, 0, [TimeSpan]::Zero)
            foreach ($file in ($stageFiles | Sort-Object { [System.IO.Path]::GetRelativePath($stageDir, $_.FullName).Replace('\', '/') })) {
                $entryName = [System.IO.Path]::GetRelativePath($stageDir, $file.FullName).Replace('\', '/')
                $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $fixedTimestamp
                $input = [System.IO.File]::OpenRead($file.FullName)
                $output = $entry.Open()
                try {
                    $input.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                    $input.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    elseif (!(Test-Path -LiteralPath $zipPath)) {
        throw "Existing-package verification requested, but the release ZIP is missing: $zipPath"
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = @($zip.Entries.FullName -replace '\\', '/')
        Assert-ExactInventory $entries $expectedEntries 'Thunderstore ZIP'

        $sourceByEntry = @{
            'manifest.json' = $manifestPath
            'README.md' = $readmePath
            'CHANGELOG.md' = $changelogPath
            'icon.png' = $iconPath
            $pluginEntry = $dll
        }
        foreach ($entryName in $expectedEntries) {
            $entry = @($zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq $entryName })
            if ($entry.Count -ne 1) {
                throw "Package ZIP must contain exactly one '$entryName' entry; found $($entry.Count)."
            }

            $stream = $entry[0].Open()
            try {
                $entryHash = Get-StreamSha256 $stream
            }
            finally {
                $stream.Dispose()
            }

            $sourceHash = (Get-FileHash -LiteralPath $sourceByEntry[$entryName] -Algorithm SHA256).Hash
            if ($entryHash -ne $sourceHash) {
                throw "Package entry '$entryName' does not match its verified source artifact. ZIP SHA256=$entryHash; source SHA256=$sourceHash."
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

$hash = Get-FileHash -LiteralPath $dll -Algorithm SHA256
$packageHash = if ($SkipPackage) { $null } else { (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash }
[pscustomobject]@{
    Status = 'STATIC_READY'
    PackageMode = if ($SkipPackage) { 'Skipped' } elseif ($VerifyExistingPackage) { 'VerifiedExisting' } else { 'Rebuilt' }
    Dll = $dll
    Package = if ($SkipPackage) { $null } else { $zipPath }
    IconSHA256 = (Get-FileHash -LiteralPath $iconPath -Algorithm SHA256).Hash
    SHA256 = $hash.Hash
    PackageSHA256 = $packageHash
}
