[CmdletBinding()]
param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\On-Together\On-Together'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $projectRoot
$assemblyPath = Join-Path $GameRoot 'OnTogether_Data\Managed\Assembly-CSharp.dll'
# dnlib 4.4.0.0 is pinned in the repo (SHA-256 C87AD741...26AA6) so a fresh clone can run this contract.
$dnlibPath = Join-Path $repoRoot 'third-party\dnlib\4.4.0.0\dnlib.dll'
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { $script:failures.Add($Message) }
}

Assert-Contract (Test-Path -LiteralPath $assemblyPath) "Current Assembly-CSharp.dll was not found at the configured game root."
Assert-Contract (Test-Path -LiteralPath $dnlibPath) 'The pinned dnlib.dll (third-party\dnlib\4.4.0.0) was not found.'
if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }

Add-Type -Path $dnlibPath
$module = [dnlib.DotNet.ModuleDefMD]::Load($assemblyPath)
try {
    function Get-TypeDef([string]$Name) { $module.GetTypes() | Where-Object FullName -eq $Name | Select-Object -First 1 }
    function Has-Method($Type, [string]$Name, [string]$Signature) {
        @($Type.Methods | Where-Object { $_.Name -eq $Name -and $_.MethodSig.ToString() -eq $Signature }).Count -eq 1
    }
    function Has-Field($Type, [string]$Name, [string]$FieldType) {
        @($Type.Fields | Where-Object { $_.Name -eq $Name -and $_.FieldType.FullName -eq $FieldType }).Count -eq 1
    }

    $text = Get-TypeDef 'TextChannelManager'
    $menu = Get-TypeDef 'MainMenuUIController'
    $panel = Get-TypeDef 'PlayerPanelController'
    $multi = Get-TypeDef 'MultiplayerManager'

    Assert-Contract ($null -ne $text) 'TextChannelManager is missing.'
    Assert-Contract (Has-Method $text 'HandleRPCGenerated_0' 'System.Void (PurrNet.RPCPacket,PurrNet.RPCInfo,System.Boolean)') 'Current generated chat relay signature changed.'
    Assert-Contract (Has-Method $text 'SendMessageAsync_Original_0' 'System.Void (System.Byte[],System.Byte[],System.Boolean,UnityEngine.Vector3,System.String,PurrNet.RPCInfo)') 'Current chat send signature changed.'
    Assert-Contract (Has-Method $menu 'ButtonChangeFilterPlayer' 'System.Void (System.Int32)') 'Lobby-size UI target changed.'
    Assert-Contract (Has-Method $menu 'ButtonChangeFilterListPlayer' 'System.Void (System.Int32)') 'Lobby-list size UI target changed.'
    Assert-Contract (Has-Field $menu '_filterPlayerText' 'TMPro.TextMeshProUGUI') 'Lobby-size text field changed.'
    Assert-Contract (Has-Field $menu '_filterVoiceText' 'TMPro.TextMeshProUGUI') 'Voice-era filter field is absent; re-audit menu topology.'
    Assert-Contract (Has-Method $panel 'UpdateServerPanel' 'System.Void ()') 'Player-panel title update target changed.'
    Assert-Contract (Has-Field $panel '_lobbyNameText' 'TMPro.TextMeshProUGUI') 'Player-panel lobby title field changed.'
    Assert-Contract (Has-Field $panel 'IsVoiceChatActive' 'System.Collections.Generic.List`1<System.Boolean>') 'Voice-era roster state changed.'
    Assert-Contract (Has-Method $multi 'CreateLobby' 'System.Void ()') 'Lobby creation target changed.'

    $plugin = Get-Content -Raw (Join-Path $projectRoot 'src\Plugin.cs')
    $rpcPatch = Get-Content -Raw (Join-Path $projectRoot 'src\Patches\TextChannelManagerPatch.cs')
    $manifest = Get-Content -Raw (Join-Path $projectRoot 'manifest.json') | ConvertFrom-Json
    $readme = Get-Content -Raw (Join-Path $projectRoot 'README.md')
    $changelog = Get-Content -Raw (Join-Path $projectRoot 'CHANGELOG.md')
    $uat = Get-Content -Raw (Join-Path $projectRoot 'UAT.md')
    $projectXml = [xml](Get-Content -Raw (Join-Path $projectRoot 'BlueSagePatched_PlayerLimitLift.csproj'))
    $pluginVersion = [regex]::Match($plugin, 'PluginVersion\s*=\s*"([^"]+)"').Groups[1].Value
    $processFilters = @([regex]::Matches($plugin, '\[BepInProcess\("([^"]+)"\)\]') | ForEach-Object { $_.Groups[1].Value })
    Assert-Contract (@($processFilters | Where-Object { $_ -eq 'OnTogether.exe' }).Count -eq 1) 'PlayerLimitLift must retain exactly one Windows OnTogether.exe process filter.'
    Assert-Contract (@($processFilters | Where-Object { $_ -eq 'On-Together' }).Count -eq 1) 'PlayerLimitLift must include exactly one extensionless On-Together process filter for macOS/Linux loader eligibility.'
    Assert-Contract ($processFilters.Count -eq 2) 'PlayerLimitLift must not add any process filters beyond OnTogether.exe and On-Together.'
    Assert-Contract ($pluginVersion -eq '1.1.8') 'Next Thunderstore upload candidate must be PlayerLimitLift version 1.1.8.'
    Assert-Contract ($pluginVersion -eq $manifest.version_number) 'Plugin and manifest versions differ.'
    Assert-Contract ($pluginVersion -eq $projectXml.Project.PropertyGroup[0].Version) 'Plugin and project versions differ.'
    Assert-Contract ($rpcPatch.Contains('nameof(TextChannelManager.OnReceivedRpc)')) 'Maintenance release no longer preserves the field-proven 1.1.1 dispatcher relay.'
    Assert-Contract ($rpcPatch.Contains('id != 0')) 'Maintenance release no longer preserves the field-proven 1.1.1 chat RPC guard.'
    Assert-Contract ($rpcPatch.Contains('__instance.HandleRPCGenerated_0(packet, info, asServer)')) 'Native generated chat handling is not preserved for ordinary senders.'
    Assert-Contract ($rpcPatch.Contains('__instance.SendMessageAsync(textBytes, userName, isLocal, pos, playerID, info)')) 'Above-15 relay must preserve the authoritative remote RPCInfo when re-broadcasting chat.'
    Assert-Contract (-not $rpcPatch.Contains('__instance.SendMessageAsync(textBytes, userName, isLocal, pos, playerID, default(RPCInfo))')) 'Above-15 relay must never make remote chat look like a host-local command.'
    Assert-Contract ($manifest.description -match '128 joinable slots plus host') 'Manifest must describe host-inclusive capacity semantics accurately.'
    Assert-Contract ((Get-Item (Join-Path $projectRoot 'README.md')).Length -le 6000) 'README must remain a compact casual-player guide.'
    Assert-Contract ((Get-Item (Join-Path $projectRoot 'CHANGELOG.md')).Length -le 24000) 'CHANGELOG must remain concise while preserving published-version history.'
    Assert-Contract ($readme -match '128 joinable slots plus the host') 'README must describe host-inclusive capacity semantics accurately.'
    Assert-Contract ($readme -match 'HOST ONLY') 'README must lead with host-only installation guidance.'
    Assert-Contract ($readme -match 'Quick Start') 'README must include a short quick start.'
    Assert-Contract ($readme -match 'ordinary joiners do not install') 'README must clearly tell ordinary joiners not to install PlayerLimitLift.'
    Assert-Contract ($readme -match 'BlueSage QoL Tweaks.*`0\.2\.5`') 'README must identify QoL 0.2.5 as the optional companion.'
    Assert-Contract ($readme -match '`ShiftSkipRate`: `16`') 'README must document the default ShiftSkipRate value as 16.'
    Assert-Contract ($readme -match 'macOS and Linux can load the mod, but still need real-world host testing') 'README must not upgrade desktop loader eligibility into unearned runtime proof.'
    Assert-Contract ($readme -match 'NickName1') 'README must preserve the common old-mod troubleshooting path.'
    Assert-Contract ($readme -notmatch 'pre-publish|runtime promotion|source parity|behavioral and rollback baseline|Published `1\.1\.5`') 'README must not expose internal release-history wording.'
    Assert-Contract ($changelog -match '(?s)^# Changelog\s+Every previous public release is kept below.*?## 1\.1\.8 — 2026-10-18') 'CHANGELOG must lead with the full-history notice and October 18, 2026 1.1.8 release.'
    Assert-Contract ($changelog -match '## 1\.1\.7 — 2026-07-30') 'CHANGELOG must preserve the July 30, 2026 1.1.7 release history.'
    Assert-Contract ($changelog -match '## 1\.1\.6 — 2026-07-24') 'CHANGELOG must preserve the prior 1.1.6 release.'
    Assert-Contract ($changelog -match '## 1\.0\.9 — 2026-06-15') 'CHANGELOG must preserve the first BlueSage PlayerLimitLift release.'
    Assert-Contract ($changelog -match 'QoL `0\.2\.4`') 'CHANGELOG must preserve QoL 0.2.4 as the historical optional companion for the 1.1.7 release.'
    Assert-Contract ($changelog -notmatch 'pre-publish|runtime promotion|source parity|behavioral and rollback baseline|Published `1\.1\.5`') 'CHANGELOG must not expose internal release-history wording.'
    Assert-Contract ($uat -match 'Release candidate date: `2026-10-18`') 'UAT must identify the October 18, 2026 release candidate.'
    Assert-Contract ($uat -match '# PlayerLimitLift 1\.1\.8 Host/Client UAT') 'UAT must identify the 1.1.8 upload candidate.'
    Assert-Contract ($uat -match '`1\.1\.8` is a pre-publish candidate pending runtime promotion') 'UAT must keep the 1.1.8 promotion gate explicit.'
    Assert-Contract ($uat -match 'runtime patch source is unchanged from `1\.1\.6`; source parity is not runtime proof') 'UAT must distinguish source parity from runtime proof.'
    Assert-Contract ($uat -match 'Published `1\.1\.5` remains the sole proven Windows behavioral and rollback baseline') 'UAT must keep 1.1.5 as the sole proven Windows baseline.'
    Assert-Contract ($uat -match 'startup reports version `1\.1\.8`') 'UAT startup expectations must identify PlayerLimitLift 1.1.8.'
    Assert-Contract ($uat -match 'Skipping .* process filters') 'UAT must reject a macOS loader run that still skips PlayerLimitLift by process filter.'
    Assert-Contract ($uat -match 'exact current QoL `0\.2\.5` candidate') 'UAT must exercise the current QoL 0.2.5 companion build.'
    Assert-Contract ($uat -match '16\+ client runtime proof remains pending') 'UAT must leave the large-lobby runtime gate pending until exercised.'
}
finally {
    $module.Dispose()
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PlayerLimitLift contracts: PASS'
Write-Host "Live assembly: $assemblyPath"
Write-Host 'Verified: chat RPC, lobby-size UI, voice-era roster fields, player-panel title, lobby creation, and version parity.'
