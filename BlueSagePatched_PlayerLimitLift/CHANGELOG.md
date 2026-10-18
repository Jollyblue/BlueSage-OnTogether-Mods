# Changelog

Every previous public release is kept below so the complete BlueSage history stays available on Thunderstore.

## 1.1.8 — 2026-10-18

- Preserves the proven host-only 128-joinable-plus-host capacity, shift-click sizing, and current in-game large-lobby chat behavior.
- Clarifies that in-game large-lobby chat stays inside On-Together and is not a Discord bridge.
- Prepares the 1.1.8 package and public guide for October 18, 2026 without changing the proven lobby behavior.
- Pairs with optional BlueSage QoL `0.2.5` without making either package a dependency.
- Keeps Windows as the tested platform; macOS and Linux can load the mod but still need real-world host testing.

## 1.1.7 — 2026-07-30

- Refreshed compatibility for current On-Together desktop builds while keeping the proven large-lobby behavior.
- Recognizes Windows, macOS, and Linux desktop process names; macOS and Linux remain runtime-unverified.
- Keeps PlayerLimitLift host-only—ordinary joining players do not install it.
- Retains startup warnings for duplicate PlayerLimit mods and safely clamped settings.
- Pairs with optional BlueSage QoL `0.2.4` without making either package a dependency.

## 1.1.6 — 2026-07-24

- Added the extensionless `On-Together` process name beside the existing Windows `OnTogether.exe` BepInEx filter, so macOS and Linux are no longer rejected before plugin startup just because the executable has no `.exe` suffix. Exactly one filter of each kind is accepted, and no other process name.
- Kept the runtime patch set the same as `1.1.5`: 128 joinable slots plus the host, shift-click sizing, native lobby-option ownership, and the field-proven above-15 chat relay.
- Windows is the tested platform. macOS and Linux can load the mod but stay unverified until real host and client logs come back. This is desktop support only, not iPhone/iPad support.
- Paired the optional companion guidance with QoL `0.2.3`; PlayerLimitLift remains standalone and host-only.
- Kept the consolidated Community Supporters section in the Thunderstore README.

## 1.1.5 — 2026-07-17

- Rebased against On-Together Steam build `24259292` / Unity `6000.0.60f1` after the July 17 native moderation update changed the local chat-render method from four arguments to five.
- Updated large-lobby chat handling for the exact current `TextChannelManager.OnChannelMessageReceived` and five-argument `AddMessageUI(string,string,bool,int,bool)` signatures, passing `sendByPlayer=true` for the host's local relayed row. Messages still transmit once and keep the original remote sender and channel.
- Added fail-open compatibility checks. If a future game build moves either chat target, PlayerLimitLift logs one clear stand-down line and leaves chat to vanilla instead of throwing a `TargetParameterCountException` after the message was sent.
- Preserved host-only capacity up to 128 joinable slots plus the host, native Public/Request/Voice ownership, shift-click sizing, and no install requirement for ordinary joiners.
- Kept PlayerLimitLift standalone; BlueSage QoL stays optional and adds its menu, Sweep, diagnostics, player-panel, and moderation layer when installed.
- Added current-signature checks and kept the host/client test checklist for above-15 chat, voice on/off, reconnect, and Steam roster agreement.

## 1.1.4 — 2026-07-17

- Preserved the 1.1.3 runtime behavior: up to 128 joinable slots plus the host, native Public/Request/Voice ownership, shift-click sizing, and correct above-15 remote chat sender details.
- Kept PlayerLimitLift fully standalone and host-only. QoL `0.2.1` remains optional; when present, its Host Health diagnostics report the PlayerLimit cap, default, shift, and chat-relay state.
- Refreshed the Thunderstore README and the coordinated BlueSage icon, which now uses a real in-game screenshot and states `ONLY HOST INSTALL NEEDED`.
- Re-ran the current-game assembly checks, release build, package inspection, and standalone and stacked diagnostics.

## 1.1.3 — 2026-07-15

- Preserved the verified large-lobby capacity, native lobby option ownership, shift-click sizing, and above-15 chat details.
- Refreshed the public icon and made the install boundary explicit: `ONLY HOST INSTALL NEEDED`.
- Kept PlayerLimitLift standalone and host-only with optional QoL diagnostics.

## 1.1.2 — 2026-07-11

- Rebased and verified compile-time targets against On-Together build `24141704` / Unity `6000.0.60f1`.
- Aligned the Thunderstore dependency pin with the tested On-Together loader, BepInExPack `5.4.2305`.
- Kept the original remote `RPCInfo` when the host re-broadcasts chat from slots above 15, so other mods can no longer mistake a remote slash message for a command the host typed. Source and built-DLL checks reject any relay that uses an empty `RPCInfo`.
- Kept the field-proven `1.1.1` large-lobby chat relay after live hosting on build `24141704` showed no `NickName1` regression. Ordinary senders keep the game's own chat handling; the compatibility decoder only handles senders above the vanilla 16-player index.
- Verified lobby-size creation: the joinable-player value you pick is stored as `count`, and the lobby receives that value plus one for the host.
- Verified the player-panel count against the voice-era roster and its membership/max display.
- Public/private, request-to-join, and voice choices stay native game settings; PlayerLimitLift does not change them.
- Added a current assembly, source, and version checker plus a focused host/client test checklist.

## 1.1.1 — 2026-06-19

- Added startup warnings when the old `officerballs PlayerLimitLift` or another PlayerLimit-style mod is also loaded.
- Added clamp warnings for `DefaultLobbySize`, `MaxLobbySize`, and `ShiftSkipRate`, so hosts can see when an unsafe value was corrected.
- Documented the optional QoL Host Health pairing: with both BlueSage mods loaded, QoL diagnostics can show the PlayerLimit cap, default, and chat-relay state.
- Refreshed Thunderstore metadata and icon placement so the Discord link stays visible under Thunderstore's badge.
- Added a clear optional companion note for `BlueSage QoL Tweaks - Beta`.
- Kept the guidance host-only: hosts use this for bigger lobbies; players only need it if they also host.
- No large-lobby packet behavior changed from `1.1.0`; this release adds startup checks and package cleanup.

## 1.1.0 — 2026-06-17

- Updated the icon and thumbnail art for the BlueSage release.
- Rewrote the README as a game-community guide: who should use the mod, who should not, how hosts use it, and what to do if you see `NickName1`.
- Clarified host-only use: only the lobby host needs BlueSage PlayerLimitLift; regular players do not need it unless they also host.
- Added a troubleshooting note: a player who sees the `NickName1` chat bug most likely still has the old `officerballs PlayerLimitLift` installed or enabled and should remove or disable it.
- Expanded the plain-language docs for first-launch config generation, `DefaultLobbySize`, `MaxLobbySize`, `ShiftSkipRate`, and Shift+click lobby-size changes.
- Added the unofficial Blue's OnTogether Discord invite to the README and the Thunderstore website link.
- Cleaned up package info, usage notes, troubleshooting flow, and credits.
- Added explicit credit and a link back to `officer_balls/officerballs_PlayerLimitLift` v1.0.8, which this community patch is based on: https://thunderstore.io/c/on-together/p/officer_balls/officerballs_PlayerLimitLift/
- This release also carries the planned `1.0.10` work, which was never uploaded on its own.

## 1.0.9 — 2026-06-15

- Rebuilt as `BlueSagePatched_PlayerLimitLift.dll` with a unique BepInEx plugin GUID.
- Updated the chat relay patch for the June 2026 PurrNet RPC signature change, fixing the Harmony failure caused by the removed `BitPacker stream` parameter on `TextChannelManager.OnReceivedRpc`.
- Preserved large-lobby chat identity for players above the vanilla 16-player limit while clamping only the UI color index to the vanilla palette.
- Added `MaxLobbySize`, `DefaultLobbySize`, `ShiftSkipRate`, and `EnableChatRelayPatch` settings.
- Added Harmony cleanup in `OnDestroy` for clean BepInEx reloads.

## Upstream History (officer_balls)

BlueSage PlayerLimitLift is a community patch of officer_balls' PlayerLimitLift. Thank you for the original mod ♥

- `1.0.8` (2026-02-01): maintenance patch; the modded tag is always applied instead of only above 16 players.
- `1.0.7` (2026-01-30): updated for the current game version to apply the modded tag instead of adding `[MODDED]` to the server name.
- `1.0.6` (2026-01-28): fixed size selector number wrapping.
- `1.0.5` (2026-01-28): added player counts to the tab, config, default lobby size, and the shift-click increment.
- `1.0.4` (2026-01-28): first Thunderstore release.
