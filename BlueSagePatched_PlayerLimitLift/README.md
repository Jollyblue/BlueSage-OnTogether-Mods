# BlueSage PlayerLimitLift

**Version 1.1.8 - October 18, 2026.**

**HOST ONLY:** lets an On-Together host create cozy larger lobbies with up to 128 joinable slots plus the host. Ordinary joiners do not install PlayerLimitLift.

## Quick Start

1. Remove or disable old `officerballs PlayerLimitLift` and any other PlayerLimit-style mod on the host.
2. Install BlueSage PlayerLimitLift on the host and start On-Together.
3. Choose the joinable-player count and create the lobby. Click changes it by one; Shift-click uses the configured step.

Players join normally without this mod.

## What It Does

- Raises the host's joinable-player setting above 16.
- Keeps the lobby-size selector and large-lobby chat working with current On-Together builds.
- Preserves the game's Public, Request to Join, and Voice choices.
- Warns at startup if a number had to be kept inside the safe range or another PlayerLimit-style mod is loaded.

**In-game large-lobby chat** means the normal On-Together chat inside a bigger lobby. It is not a Discord bridge and does not send players' messages outside the game.

## Defaults

- `DefaultLobbySize`: `16`
- `MaxLobbySize`: `128`
- `ShiftSkipRate`: `16`
- `EnableChatRelayPatch`: `true`

The config is created after first launch at `BepInEx/config/com.bluesage.ontogether.playerlimitlift.cfg`.

## If You See NickName1

If a joining player's chat name shows as `NickName1`, remove the old `officerballs PlayerLimitLift` from that player's client, restart On-Together, and rejoin. They do not need BlueSage PlayerLimitLift unless they also host.

## Optional QoL Companion

BlueSage QoL Tweaks `0.2.5` is a separate optional package for players and hosts. It adds chat, styling, Focus Pulse, reconnect, player-panel, and host-health tools; neither package requires the other.

## Compatibility and Support

Windows is tested. macOS and Linux can load the mod, but still need real-world host testing.

If capacity or chat behaves unexpectedly, remove duplicate capacity mods, restore the defaults above, restart, and test a small lobby first. When asking for help, include the game version, selected lobby size, whether the host or a player saw the issue, and the relevant startup lines after reviewing them for private information.

Community Discord: https://discord.gg/JujMEwtN3q

BlueSage website: https://bluesagetech.net/ (Coming Soon)

Based on the original `officerballs PlayerLimitLift`, with thanks to officer_balls, Arashi_Lei, and earlier contributors.

Made by Blue + Sage = BlueSage with superpowered neurodivergence ♥
