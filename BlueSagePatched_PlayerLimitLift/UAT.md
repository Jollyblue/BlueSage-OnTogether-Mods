# PlayerLimitLift 1.1.8 Host/Client UAT

Release candidate date: `2026-10-18`.

Use a disposable release-candidate DLL from `bin`, not a published or known-good package. Keep account identifiers and raw player lists out of screenshots and reports.

`1.1.8` is a pre-publish candidate pending runtime promotion.
Its runtime patch source is unchanged from `1.1.6`; source parity is not runtime proof.
Published `1.1.5` remains the sole proven Windows behavioral and rollback baseline.
The field-proven `1.1.1` chat relay remains in source, and the generated-handler-only refactor is not included.

## Proof boundary before testing

- Windows has a runtime-green published `1.1.5` baseline; the exact `1.1.8`
  candidate bytes still require this runtime gate before promotion.
- macOS and Linux are loader-eligible but runtime-unverified.
- The 16+ client runtime proof remains pending until a real 16+ lobby or an
  approved deterministic multi-client harness completes the checks below.

## Host setup

1. Confirm only `BlueSagePatched_PlayerLimitLift` is loaded; disable old `officerballs PlayerLimitLift` and other capacity mods.
2. Confirm startup reports version `1.1.8`, cap `128`, default `16`, and no Harmony target failures.
3. In the native lobby creator, exercise Public, Request to Join, and Voice independently. PlayerLimitLift must not change any of those selections.
4. Verify normal click changes capacity by one, Shift+click changes it by `ShiftSkipRate`, and both directions wrap without zero/negative values.
5. Create at 16, 32, 89, and 128 joinable-player selections. Verify Steam/PurrLobby capacity is the selection plus the host and the lobby browser `count` metadata remains the displayed joinable capacity.

## Host/client behavior

1. Join with an unmodded client and a client running the exact current QoL `0.2.5` candidate. Neither client should require PlayerLimitLift.
2. Confirm roster title membership/max values remain correct as players join, leave, disconnect, and reconnect. Ghost rows must not increase authoritative membership.
3. Exchange chat among host, vanilla-index clients, and at least one player whose roster index exceeds 15. Confirm names and message bodies are correct with no `NickName1`, duplication, or dropped messages.
4. With Voice disabled, confirm no voice UI/runtime regression. With Voice enabled in a separate lobby, confirm mute/activity controls and speech continue working while above-15 chat still relays.
5. Toggle `EnableChatRelayPatch=false`, restart, and confirm native <=16 chat still works; do not claim >16 chat support in this diagnostic state. Restore `true` afterward.
6. Run QoL reconnect/host-rehost UAT with PlayerLimitLift present. Confirm replacement-lobby capacity and authoritative roster count converge after reconnect.

## macOS and Linux loader gates

1. Use the same `1.1.8` package on separate macOS and Linux Steam installations with the current Mono BepInEx pack. Confirm each process header identifies `On-Together`; do not rename the DLL or remove the Windows process filter.
2. Confirm each BepInEx log contains `BlueSagePatched PlayerLimitLift 1.1.8 loaded` and does not contain `Skipping [BlueSagePatched PlayerLimitLift 1.1.8] because of process filters`.
3. Host a disposable lobby, change the joinable-player selection, and complete the normal host/client checks above with at least one Windows client.
4. Capture the macOS and Linux logs privately for review. Loader eligibility alone is not runtime proof; each platform remains runtime-unverified until its complete gate passes. This does not claim iPhone or iPad support.

## Failure gates

- Any process-filter skip, Harmony `undefined target`, RPC decode warning, duplicate chat, incorrect member/max count, changed voice/request/public selection, or client requirement blocks release.
- Do not publish, upload, or replace known-good DLLs until all applicable checks pass and the raw test log has been reviewed for identifiers before sharing.
