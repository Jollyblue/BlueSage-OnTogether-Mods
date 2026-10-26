# Changelog

Every previous public release is kept below so the complete BlueSage history stays available on Thunderstore.

## 0.2.7 — 2026-10-25

What two long public hosts taught us: no more reconnect loops, the Report shows the game's own faces and badges, the stats file stops churning, the log says things once, and the automatic cleanup goes quiet. Plus one small thing: the `[U]` badge now tells you which QoL version a player runs.

### Players
- **New: local chat reaches farther on your screen.** The game only shows a local line when its sender is within 5 m. You now see lines from up to 15 m by default. Choose 5 to 25 m in the QoL menu (**Local chat range**); 5 is the game's own range. This only changes what you see: nothing is sent to anyone else, global chat is unchanged, and your ignore and mute lists still apply first. The speech bubble above the speaker's head follows the same range, so a line you can read always has its bubble.
- **New: more of what you do answers with a toast.** Saving a setting in the QoL menu or Style Helper, applying your name or status, the WORK and RELAX buttons, hiding the stats bar, Report corrections, and chat commands that change something (like `/timestamps`, `/status`, `/rebind`) now show a small note, the same way the On/Off switches always did. Information commands such as `/qol status` stay in chat. Being made a Helper, or losing it, also gets a note. With Toast Pop-ups off, nothing pops up and the chat lines stay as they were.
- **New: your extra outfit slots are easier to find.** Styles 4 to 9 sit below the game's three in the avatar screen's style list and start on. The first time you open that screen, one note points them out. The QoL menu switch is now called **Extra Outfit Slots 4-9**, and the in-game guide, README and tooltips say where they are and that your saved outfits wait for you if you turn them off.
- **Fixed: toasts show again.** An earlier 0.2.7 test build drew none; they appear as they did in 0.2.6.
- **New: hover `[U]` to see a player's QoL version**, like `QoL v0.2.7 User`. Players on 0.2.6 or older show as `Pre-indicator QoL User`, because those versions never said which one they were. The little `[U]` itself is unchanged, so narrow player lists look the same, and nothing about who can do what changed.

### Reconnect
- **Fixed: a lobby that is gone can no longer hold you in a reconnect loop.** When a host's game stays up but Steam drops its lobby, every careful rejoin used to get in, fall out, and start again. Reconnect Guard now gives up after 5 tries in a row on the same lobby, shows a short note, and leaves you on the menu. Two minutes of normal play after any rejoin, or joining a different lobby, gives it a fresh count.
- **New: hosts hear when Steam drops the lobby.** One note the moment it happens: new players cannot join and host tools are locked until you re-host.

### BlueSage Report
- **Fixed: the Journal faces were missing when you opened the Report from the QoL menu.** They only showed after the game's own Statistics tab had been opened once. The Report now finds the faces itself, and if it ever cannot, the log says why.
- **New: the game's own icons where they fit.** Each achievement shows its badge (dimmed until you unlock it), pomodoro rows carry the pomodoro icon, and the Work and Relax filters show the pomodoro and break icons. Everything scales with your BlueSage menu scale, so 1080p, 1440p and 4K read the same.

### Work / Relax tracking
- **Changed: history is saved the way you work, not minute by minute.** The whole file used to be rewritten every minute (about 5 MB a minute on a long host). Now one pomodoro is one row, one activity stretch is one row, the file saves every five minutes, and old days fold into daily rows while you play. Totals and the Report are unchanged; the first start after this update tidies the old minutes once, and the Report opens faster on a long history. If you go back to 0.2.6, it starts a fresh Work / Relax history; unzip `archive/<date>-pre-0.2.7-layout.zip` into BepInEx to bring the old one back.

### Hosting
- **Fixed: the host journal no longer fills up with the automatic 15-minute player-list check.** Over a long session those rows pushed real kicks, mutes and bans out of the box. The check still runs; it writes nothing unless something changed. Your own refresh button still records its row.

### Smaller files on your PC
- **New: one tidy folder.** Everything BlueSage writes now lives in `BepInEx/BlueSageQoLModSuite`: your trackers, the optional logs, and an `archive` of older files packed into zips you can open with a double-click. Files are named by date and time, and a short `README.txt` explains each part. The first start moves your old files in once (you get one small note), and a copy of the old folders waits in the archive for 14 days. Your game saves, settings files, avatar slots and chalkboards are never moved.
- **Changed: older files tidy themselves.** Logs keep your last 3 sessions, 3 days or 24 MB, whichever comes first; older ones are zipped, and zips leave after 14 days or 48 MB. Whatever happens, the whole BlueSage folder never stays over 1 GB: the oldest files go first.
- **Changed: the host journal stays under 1 MB.** It notes joins and leaves once every five minutes instead of on every change, and full pages go into the archive.
- **Changed: Work / Relax history is less than half the size.** Each focus minute is stored once; Today, Week and Month are worked out from your totals. Past manual tweaks to your Today, Week or Month numbers no longer apply; the ones on your totals still do.
- **Changed: the optional local chat and log copy (Local Audit Exports) is off unless you turn it on.** It is switched off once for everyone; turn it back on in the QoL menu or with `/auditlog on`.

### Calmer log, same safety
- **Three lines that repeated for hours in a busy lobby now appear once:** the player-matching wait (one line when it starts, one reminder an hour, one when it clears), the player-check state (a change has to last a minute before it is logged), and the 5-minute lobby refresh (only when something other than the player count changed). What the mod does is unchanged; only what it writes.
- **New: one session summary line.** At quit and every six hours, `[BlueSageSession]` writes uptime, peak players and joins (hosts), cleanups run, the stats file's size and write count, journal rows, memory at start and now, and how many warnings and errors the mod logged. One line to paste when something feels off; no names, no ids.
- **New: the cleanup line shows the game engine's own memory numbers** before and after each cleanup, so the next release can decide the automatic cleanup's future from numbers rather than guesses. A number this build cannot read shows `n/a`.
- **Changed: the automatic cleanup no longer shows a toast every 40 minutes.** It was noise in a cozy session. The note is switched off once for everyone; turn **Sweep Chat Note** back on in the QoL menu if you liked it. A cleanup you start yourself still confirms.

PlayerLimitLift is unchanged. Made with love by Blue + Sage = BlueSage ♥

## 0.2.6 — 2026-10-20

Round 4: your outfits stay yours, moderation hits the right player, hosts get a journal, and the autosave hitch is gone.

### No more freeze when the game autosaves
- **Fixed: the game froze for a moment every 3 minutes, when it autosaved.** 0.2.5's Work / Relax tracking wrote its whole history file, synced to disk, inside the game's own autosave. It now writes a copy a minute later on a background thread, and only the final save when you quit syncs to disk. Thank you to the player who reported it with a log.
- **The history file stops growing on its own.** Idle time used to add rows at every autosave; same-day rows now merge and days older than two fold into daily totals, so the file stays small and BlueSage Report's totals are unchanged. The first start after this update may pause once while an old, large history file is tidied.
- **Automatic Cleanup moved to every 40 minutes and waits for a quiet moment.** It never starts right before or after the game's 3-minute autosave or its 30-minute cleanup, so the two can't stack. If an earlier update had set you to 10 or 15 minutes, you're moved to 40 once; any other value you chose stays.

### Avatar slots 4-9 keep your work
- **Game outfit changes stay in the slot you're wearing.** Drinking a lemonade or closing the avatar panel while slot 4-9 is on now saves into that slot; vanilla slots 1-3 never change on their own.
- **Your extra slots are stored crash-safe.** They live in their own file with three rotating backups; a crash or power loss mid-save can no longer empty them, and the game's save file is never touched.
- **Rollback-friendly.** If you go back to 0.2.5, edit a slot and come forward again, the edited slots win; the previous slots are copied aside first.
- **Honest when something is wrong.** If the slot file cannot be read, you get one in-game notice that slots are read-only this session, and nothing on disk is changed.

### Moderation hits the exact player
- **The game's own Ban buttons go through the host check.** The ID card's Ban, the player row's Ban and the report confirm all use BlueSage's live-row check when you host, so a player who rejoined through a reconnect mod is removed instead of a stale row. Refusals (protected victims, unclear rows, empty ids) say why; nothing is guessed.
- **Reconnected players can be banned.** Rows that are in the game but missing from Steam's lobby list are labeled RECONNECTED and act normally.
- **Loading players can be banned.** A player who is in Steam's lobby but has no live connection yet is banned without a kick and refused the moment their connection completes.
- **Helpers keep working after an automatic reconnect.** A Helper who is in the game but no longer in Steam's lobby list used to be told to rejoin before acting. Now the request travels over the game connection; the host checks it against that Helper's live connection (and the Steam-authenticated ID when the game exposes it), acts or refuses it in the journal, and notes "issuer not in Steam list". The Helper gets no reply either way. On a host with an older BlueSage nothing is sent and the Helper is asked to rejoin, as before.
- **Kick.** `/sidkick` (or the Host Tools row buttons): the player is removed through the game's own flow and the ban lifts itself once they have left, within 20 seconds. If they are still here at 20 seconds the ban stays and you are told. Helpers can kick through the host.
- **Lobby-wide mute.** `/sidmute` stops the host relaying that player's chat to everyone for this hosting session (they will notice their own lines stop too); `/sidunmute` lifts it. Voice is unaffected.
- **Opening a profile never fails closed.** The ID card and Steam profile open for every live row, with the trust level shown.

### Hosts get a journal
- **Host journal, always on while you host.** Every ban, unban, kick and mute (yours, a Helper's, or the game's own buttons) with who, what, whom, the RECONNECTED/LOADING label and the result, plus a status line when the lobby picture changes, in Host Tools and in `BepInEx/BlueSageAudit/host-journal/host-journal-v1.jsonl`. Guests write nothing.
- **Built for big lobbies.** The journal box shows your actions first, one per line; a pinned line shows the lobby picture now; Status and Refused-only views are one click; hover a row for the Steam ID and reason, click to copy, or copy the last 20. A 100-slot lobby's joins and leaves never bury a ban.
- **Automatic 15-minute refresh while hosting** relabels the player list from already-available state and notes it in the journal. It never moderates on its own.

### Calmer logs and small comforts
- **Deep sweep diagnostics removed.** The cleanup log is one short line again, and the sweep stays within the game's own cleanup timing.
- **Fewer repeated warnings.** Identity-coverage and host-health lines are written when something changes, not on every tick.
- **Compatibility heads-up.** If another Helper-like mod (TogetherPlus, Alpha) is loaded, one local note appears once per session after the welcome pop-up.
- **Badge tooltips cost less every frame**; the Steam runtime check runs once a second instead of every frame.
- `/bluedevdebug` is gone; typing it now only shows a local pointer to the menu's Local Audit snapshot.

Updated QoL and PLL with love as Blue + Sage = BlueSage ♥

## 0.2.5 — 2026-10-18

A big comfort update: new ways to open every tool, fishing help for sore hands, friendlier hosting, and a calmer, easier-to-read menu. Everything new can be switched off, and choices you already saved are never overwritten.

### Open things your way

- **Click to open the menus.** With the Performance Overlay on, two buttons sit just above the FPS / PING / RAM bar: **QoL-Menu** and **Styler-Menu**. One click opens or closes each one, so you never need a hotkey or command.
- **Change any hotkey inside the game.** QoL Menu (Home), Style Helper (Insert), the new Fly key (End) and manual cleanup (F8) can all be changed in the menu's Hotkeys section or with `/rebind`; `/keys` shows them at a glance. No config files, ever. It only offers keys that can't end up typed into chat, refuses keys that are already taken and tells you why, and **Reset All Hotkeys to Defaults** puts everything back in one click.
- **Hotkeys wait while you type.** While chat or any text box is in use, no hotkey fires, so writing a message never flips something by accident.
- **A welcome pop-up every time you join** a lobby, reconnects and lobby-hopping included. It shows a short how-to with your current hotkeys. **Got it** closes it for that visit; **Don't show this again** turns it off, and `/welcome` or the Welcome Overlay switch brings it back.
- **The chat welcome greets you on every join too**, with a link to the full README on Thunderstore.
- **A cozy guide.** `/bsqol`, `/guide` or `/faq` opens a readable guide to everything BlueSage does. The welcome pop-up has buttons for the guide and the README.

### On-screen toasts

- Short pop-ups confirm what just happened: hotkeys, every menu On/Off switch, hotkey changes, reconnect steps, safety checks, cleanups (ours and the game's own 30-minute one) and host news. They're easy to spot even when chat scrolls fast, and identical ones don't pile up.
- Choose how long they stay (2–15 seconds, default 3.5) and how big they are (Auto grows them on bigger screens, or pick 75–200%) in Customize > General.
- Switch them off with **Toast Pop-ups** in Toggles.

### Fishing Aid

- **For sore or unsteady hands, off until you turn it on** in Toggles > Accessibility (the purple section) or with `/fishaid`.
- Take out your rod with H near water and stand still. It looks for water all around you, casts, answers bites, plays the reel mini-game, presses Release (or Recycle) a moment after each catch, and casts again until you put the rod away.
- Cast once by hand and it remembers that spot. If it can't find water after 20 quick tries, it tells you and hands control back.
- Its settings sit right under the switch: bite reaction (default 0.7 s), clicks per second for the last round (default 7) and the release delay (default 3.5 s). Those defaults come from long play sessions and feel right for most people.
- Its reminders show each time you take the rod out, not just the first time.
- **Reusable Consumables now keeps fishing bait too**, once you own it.

### For hosts

- **Lobby welcome message.** Set it with `/motd` or in the roomy box in **Host Tools** (with a live character count), so only hosts see it. Players with QoL see it as a pop-up and a chat line when they join. If you don't set one, it says "Welcome to my server!".
- **Announcements.** Hosts and Helpers can send a quick pop-up to everyone with QoL using `/announce <message>` (Host Announcements switch, on by default).
- **Steam ID check.** The host compares every player's Steam ID with the one Steam itself confirms for their connection, so nobody can pose as someone else's account. Helpers see the host's results.
- **Host Tools** (the new name for the host view) explains each health notice and walks you through a step-by-step player-matching repair path: refresh the list first, recheck matches only if needed, then rejoin or recreate the lobby if things still don't line up. Extra details stay folded away, the moderation-button check only looks and never acts, and in-game large-lobby chat is clearly separate from Discord.
- Host Health shows your player count the way the game does (for example 1/111) and lists PlayerLimitLift's maximum separately.

### Work / Relax and BlueSage Report

- A **WORK / RELAX** choice sits beside FPS / PING / RAM. Typing is optional: `/workon`, `/relaxon`, `/workoff`, `/relaxoff` and `/mode work|relax` do the same.
- **BlueSage Report** has its own cozy, resizable window that splits the game's saved statistics by what you were doing. It opens without stutter, starts older totals as Unclassified, and lets you correct or undo a session.
- It uses the Journal's five face pictures with plain labels: **Great**, **Good**, **Okay**, **Low** and **Rough**.
- The game's own statistics, Journal entries, feelings, achievements, rewards and streaks are never changed.

### Improved

- **Reconnect Guard waits before rejoining** (20 seconds by default, 10–120 in Settings) so the host can clear your old connection and the player count stays right. It stops if you join somewhere else or switch the guard off.
- **Cleanup** posts a quiet chat note when an automatic or manual cleanup finishes (NotifyOnSweep, on by default). An extra memory pass for very long sessions in big lobbies is available too (ForceManagedGcCollect, off by default). F8, the QoL menu and the sweep slash commands still work whenever you like.
- **Fly mode** has finer Alt precision (adjustable) and turns the camera with the middle mouse button (changeable).
- **Extended Camera Zoom** scrolls much closer and farther than normal, walking or flying (on by default; desk view unchanged). Scrolling inside a BlueSage window no longer zooms the camera.
- **Player lists** stay light in lobbies of 30–40+ people, fix names and buttons after joins and leaves without ever guessing, and keep the small `[U]` marker for confirmed QoL players. It's a friendly hint, never a moderation role.
- Chat ID cards stay with the person who wrote the message, and clicking someone on the minimap opens their ID again for people who joined late.
- **Menus are easier to read:** Customize uses two balanced columns on wider windows, setting names and tooltips no longer get cut off, Save buttons fit their labels, menu size scales calmly from 75% to 200%, and a **WEBSITE** button for https://bluesagetech.net/ (Coming Soon).
- **Fresh installs start with Blue's own setup:** Community Safety and the Performance Overlay on, Spoons off, Auto menu size, Midnight Dock, 4000 characters for chat, profile and room text, and Automatic Cleanup every 15 minutes. Anything you already chose stays yours.
- Supporter credits are up to date. Thank you!

### Retired

- **Player Render Saver is retired for now.** Hiding far-away players caused visual glitches, so everyone draws normally again.

Focus Pulse, readable names, status and Spoons tag, cleanup, reconnect help, accessibility and the game's own moderation keep working as before.

Prepared with love by Blue + Sage = BlueSage ♥

## 0.2.4 — 2026-07-30

- Keeps names, status, and Spoons tag complete whenever the row has room, improves role-label colors and visibility, and adds safe pooled-row cleanup.
- Opens the exact clicked player's portrait and binds chat actions to the exact current player; stale or ambiguous matches fail closed.
- Reuses On-Together's native **Sweep in progress** presentation for BlueSage cleanup.
- Adds a unified Host Console with 24/7 readiness, PlayerLimitLift status, native moderation audit, and safe exact-target controls.
- Mirrors host-assigned Helper Moderator grants for backward-compatible trusted Rescue receivers while keeping Rescue controls private and account-gated.
- Keeps F8, the QoL menu, and sweep slash commands available for explicit cleanup.
- Keeps Focus Pulse, chat, styling, reconnect, accessibility, and cleanup behavior user-configurable.
- Updated QoL and PLL with love as Blue + Sage = BlueSage ♥

## 0.2.3 — 2026-07-23

0.2.3 was built and tested in several rounds between July 19 and July 23. Everything those rounds changed is gathered here in one entry.

### Game Update Compatibility

- Revalidated On-Together Steam build `24259292` / Unity `6000.0.60f1` against the current game libraries before changing any release code, including the chat, ID-card, report UI, PurrNet despawn, and Vivox surfaces.
- Added a macOS loader path beside the Windows one: the plugin now accepts both `OnTogether.exe` and the extensionless `On-Together` process name seen in a Mac BepInEx log. Mac support is best effort until it is tested on a real Mac, and this is not iPhone/iPad support.
- Repaired the `0.2.2` Vivox compatibility warning by targeting build `24259292`'s `VivoxServiceInternal.Set3DPosition(GameObject, string, bool)` overload. The optional position-rate guard still fails open if the target is missing.
- Restored cyan, underlined chat links on build `24259292` by finding its generic `TMP_Text` chat labels instead of requiring the older `TextMeshProUGUI` component.
- Kept optional Harmony startup failing open per patch class, so one future API change cannot take down unrelated BlueSage menus, commands, Sweep, or controllers.
- Left the game's own teardown path alone after confirming that the `PlayerCustomizationController.OnDestroy` null reference comes from the game's `MaterialPool` lifecycle. BlueSage never skips the original method or its base cleanup.
- Checked native `PetController.Start`, RectTransform-parent, monitor/window, and TMP input log noise against the current game. These lines are tracked but are not blamed on BlueSage without a matching cause.

### Player List, Roles, and Identity

- Added compact, hover-explained role markers: `[O]` Owner/Host, `[M]` Moderator/Helper, and `[U]` Compatible QoL User. They sit in their own overlay at the end of the row, so BlueSage never resizes, truncates, or moves the game's player names. Markers are labels only and never grant host, Helper, moderation, or Clone Shield authority.
- Rebuilt the role tooltip with a near-opaque midnight background, white text, readable padding, and plain-language Owner/Host, Moderator/Helper, and Compatible QoL User descriptions.
- Added local `(Helper)` decoration from the host's fresh compatible capability and exact-ID Helper roster for every compatible QoL client. Role labels decorate only your own view of that exact row, never change replicated names, Steam persona, ID-card data, host ownership, or moderation, and clear on owner or lobby change, removal, expiry, bad data, row reuse, or unload.
- Kept `LIVE / STEAM CHECK` realistic and light. The profile still opens while a live row is missing from current Steam membership, identity-sensitive moderation stays locked, and the row heals itself once the sources agree again.
- Normalized PurrNet player identifiers before matching them with Steam and native rows, so row actions, ID-card helpers, chat-sender context, minimap identity, and `/idsync` all compare one consistent value. Unresolved or duplicate evidence still fails closed.
- Fixed an inverted check that treated valid negative Unity instance IDs as missing (`0` is the real missing value), which had been throwing away good roster snapshots.
- Pointed the player-row badge observer at the existing cached roster snapshot instead of rebuilding identity evidence every time Unity recycles a row, and cut the read-only runtime observer from ten polls per second to two with no per-player string allocations.
- When a roster snapshot is incomplete, the reason now says what is missing instead of showing an empty message.
- Added Host/Helper-only `/idsync` (aliases `/identitysync` and `/rostersync`) to repair broken, stale, or incomplete Steam/PurrNet row matches. It leaves working rows alone, takes at most one snapshot behind a five-second throttle, starts no polling loop, and ordinary players get a read-only status.
- Each player row regains exactly one `@` helper and one `ID` helper, re-resolved for that exact row at click time. The `ID` helper opens the game's own ID card. When an active Pomo sync control owns that space, QoL steps aside instead of crowding it, and the helper lane now sits slightly further left so the PomoSync button does not cover it.
- Kept moderation and profile evidence attached to the exact game-owned Add Friend/player row. BlueSage refreshes only its own helpers and never hides, moves, or replaces the game's profile, moderation, Helper, rescue, ID-card, or Pomo controls.

### Chat

- Chat copy and link actions live on the game's own message button: a plain left-click still opens the game's ID card, Ctrl-left-click copies the message text, and Shift-left-click runs the guarded link copy/open flow.
- Right-click actions work alongside them where Unity delivers right-clicks: plain right-click copies the message, Shift-right-click runs the guarded link flow, Ctrl-right-click copies a short identity record for that exact sender, and Alt-right-click opens the sender's game ID card. Rows with several modifiers held, system rows, and stale or unresolved rows stay locked.

### Menus and Performance Overlay

- The QoL Menu and Style Helper can be moved and resized independently again. Opening Style Helper from the menu no longer forces a paired layout or replaces its saved size and position.
- Standardized QoL Menu and Style Helper framing, footer help, tooltips, and editable-field descriptions.
- Added the **Performance Overlay**: a draggable one-line FPS/PING/RAM readout built into public QoL with a normal On/Off row in the QoL Menu. Hotkeys and the close button on the bar are optional shortcuts.
- Hosts see `PING --` honestly, while connected players see their live round-trip latency when it is available.
- RAM readings work on Windows even when the managed process counters return zero.
- Refreshed slash commands, typeahead, tooltips, lobby guidance, and first-login tips for build `24259292`'s menus, chat, player panel, ID card, Pomo, and Helper surfaces. Tips stay local and never change game or moderation state.
- Simplified Community Supporters into a privacy-respecting thank-you directory: one entry per supporter, and one click shows their public messages. Tester credits stay separate.

### Sweep and Stability

- Automatic Sweep follows real time and runs single-flight: unfocused sessions keep counting down, a shorter saved interval pulls the next run forward, a longer edit cannot postpone a run that is already due, manual runs leave the automatic schedule alone, and the next interval starts only after Unity's asynchronous unload finishes. Sweep is pressure relief, not a root-cause fix for the game's memory growth.
- Added shutdown ownership and crash markers so quitting and destroying cannot clean up Steam or local state twice. The markers hold no Steam, lobby, player, or chat identity.
- Every active rescue follow is now tied to the exact lobby, owner, issuing Host or Helper, and the player's long-haul opt-in. If any of those change, the follow stops immediately.
- Hardened the package privacy checks across archive entry names, text, readable binary strings, and both UTF-16LE alignments. The scanner reports counts only and never prints what it matched.

### Community Safety and Chalkboards

- Community BanData refresh is now off by default and opt-in for any QoL client through the menu or `/communitybans on`. It only adds missing entries, keeps every personal row, rolls back pending additions if saving fails, and never kicks anyone. A one-time migration resets the old host-only toggle first.
- A source with any invalid or duplicate SteamID64 is now rejected whole instead of half-merged, and imported nicknames are stripped of control characters, TMP angle brackets, and text-direction controls before they are stored.
- The casual menu shows a plain-language Community Safety List status instead of a raw data dump. Detailed evidence stays in diagnostics and the local audit log.
- Added exact-board chalkboard tools under `/chalk` (`/chalkboard`). `/chalk boards` lists the three live boards on build `24259292`: `0` River/Stage, `1` Classroom face A, and `2` Classroom face B. A fourth index is rejected instead of guessed. Hosts can run `/chalk clear <index>` directly; authorized Helpers send a short-lived request that the Host fully rechecks first.
- Board clears are recoverable: BlueSage saves a local recovery snapshot before changing a board, restores it if the replacement fails, and sends compatible QoL players the full board state. If a rollback ever fails, further destructive board actions lock.
- Renamed the casual control to **Chalkboard Recovery** and explained the three boards, local recovery, and the Host/Helper boundary in plain words.
- Chalkboard attribution stays honest. The game does not record who drew old pixels, so BlueSage never auto-clears a board after a ban. A ban only reminds Hosts and Helpers to check `/chalk boards` and choose the exact board to clear.

### PlayerLimitLift

- Paired with PlayerLimitLift `1.1.6` for hosts. Windows is fully supported; macOS can load it but is not yet tested, and iOS is not supported.
- Kept staged config migrations, the `0.2.1` one-time Sweep safety migration, every saved preference, and personal native ban lists intact.

## 0.2.2 — 2026-07-17

- Rebased against On-Together Steam build `24259292` / Unity `6000.0.60f1` after the July 17 native moderation and ID-card update changed game-owned chat and profile method signatures.
- Updated every `TextChannelManager.AddMessageUI` patch to the current five-argument contract and preserved the new `sendByPlayer` value. This restores the sender's own local message row without duplicating the network send or changing remote sender/channel metadata.
- Replaced all stale `TemporaryPhoto.ButtonOpenIdCheck(NetworkTransform)` calls with the current integer roster-index path. BlueSage continues to enrich exact identity evidence while the game's new mute/report/remove UI remains native and authoritative.
- Made Harmony startup fail open per patch class. One optional target moving in a future game update now produces a clear compatibility line and disables only that patch instead of aborting plugin startup, Home/Insert menus, slash-command typeahead, Sweep, and the remaining QoL controllers.
- Added current-binary regression contracts for the five-argument chat path, sender rendering flag, integer ID-card path, and fail-open startup boundary.
- Preserved the 0.2.1 single-flight Sweep, 10-minute one-time migrated default, 5–180-minute user range, resource/process evidence, roster-registration guard, Vivox rate guard, local audit allocation fix, saved preferences, and public/private authority boundaries.

## 0.2.1 — 2026-07-17

- Added verified-host-only maintenance for `andrewlimforfun/ot-community-banlist` `BanData.txt`. The bounded HTTPS fetch validates paired rows and appends only missing SteamID64 entries to the game's native list; every existing personal ID/nickname remains byte-for-byte untouched, malformed or failed fetches make no change, save failure rolls back only pending additions, and diagnostics expose counts rather than identities. This does not create a public player badge or weaken exact live preview/confirm authority.
- Made roster completeness bidirectional. BlueSage no longer reports identity alignment recovered when every Steam member maps to a native row but one or more live native/PurrNet rows are absent from current Steam lobby membership; Clone Shield and identity-sensitive actions remain fail-closed until both directions agree.
- Reworded `LIVE / STEAM CHECK` as a factual cross-roster disagreement instead of a temporary promise: the game/PurrNet row is live, its SteamID is absent from the current Steam lobby member enumeration, the profile may still open, and identity-sensitive actions stay locked. A bounded transition-only `[BlueSageIdentityCoverage]` line records counts and state without names or SteamIDs.
- Preserved exact rescue boundaries during that disagreement. Public QoL still accepts authenticated `v2` Steam addressing and the already-supported `v3` unique native-PlayerID addressing, but each receiver validates its own exact local identity, sender authority, freshness, lobby, and nonce before moving only its local owner.
- Made Asset Sweep single-flight and completion-aware. BlueSage now waits for Unity's asynchronous unload operation before optional GC, completion notification, and the next sweep; compact UTC start/completion/failure lines record sequence, trigger, elapsed time, and managed-memory delta without player or lobby identities.
- Added automatic per-sweep Unity resource evidence for ordinary users: each completion records pre/post Texture2D, RenderTexture, Material, Mesh, AudioClip, and loaded-GameObject totals, the dominant reclaimed category, and up to five largest reclaimed sizes labeled only by resource category. The capture is bounded, runs only around a sweep, and requires no command or manual capture. BlueSage's added fields contain no player/chat/lobby identity; the complete `BepInEx/LogOutput.log` can still contain unrelated game or plugin output and should be reviewed before public posting.
- Added low-cost process/session context to that same completion line: private/working-set deltas, handle/thread counts, native-row versus Steam-member totals, and voice-manager/off/joined/queued state. These are aggregate diagnostics only—no lobby code, SteamID, player name, chat content, or voice identity is recorded.
- Added a privacy-respecting thank-you for an anonymous supporter to the in-game and README community credits.
- Kept automatic Sweep enabled at the one-time migrated 10-minute default with the existing 5–180 minute range. Later intentional choices remain untouched.
- Added a surgical host-side registration guard around the game's current player-roster append path. Invalid rows and exact transform duplicates are rejected; a replacement for the same stable identity removes only the old parallel-list row before native registration. When an old Vivox row conflicts with the incoming Steam identity, exact SteamID64 wins so replacing the wrong row cannot leave a duplicate Steam entry. No network object, avatar, pet, or game-owned material is destroyed by this guard.
- Added a default-on, fail-open Vivox position-rate guard that coalesces the game's per-frame 3D-position request to 4 Hz and bounds tracking state. This reduces a verified secondary native/allocation pressure path but is not presented as the root cause of voice-off growth.
- Replaced the local audit mirror's idle one-second 512 KiB allocation with a reusable 64 KiB buffer allocated only when unread bytes exist.
- Made shareable BepInEx diagnostics privacy-safe by default: UTC timestamp, counts, state, and roster fingerprint remain; exact lobby IDs, Steam IDs, personas, titles, tags, and player lists stay in authorized local audit evidence only.
- Live build-24141704 evidence now separates mitigation from cause. An unmodded guest reproduced the catastrophic Blue-lobby growth, while private and comparison lobbies did not; Vivox-off host samples still grew. A paired manual Unity-resource capture observed Texture2D fall from 29,263 / 3.60 GB to 2,888 / 192.64 MB after `Resources.UnloadUnusedAssets()` with the same scene-object count. This proves a large unused Unity-texture population was awaiting reclamation, not which system created it. Reverse traversal confirms a separate roster replacement defect and per-frame Vivox positional requests as investigation lanes; neither is presented as the complete texture owner.
- Preserved every public QoL, host/helper authority, moderation, reconnect, style, chat, movement, and PlayerLimit compatibility boundary from 0.2.0.

## 0.2.0 — 2026-07-15

- Live 2026-07-15 crowded-lobby sampling confirmed that `Resources.UnloadUnusedAssets()` completes asynchronously and reclaimed roughly 12.5 GiB of private commit plus 5.9 GiB of dedicated VRAM in the observed host session. Automatic Sweep now defaults to 10 minutes, supports a 5–180 minute range, and forces existing profiles on at 10 minutes exactly once before respecting later intentional choices.
- Restored the established fake-danger `DO NOT PRESS` button and tooltip for the harmless optional lobby joke. Its first press can still send only one playful line; repeat presses remain local and it never changes moderation or game state.
- Kept Style Helper footer guidance owned by its active Name, Status, or Templates surface so opening `Insert` after another IMGUI window cannot inherit unrelated menu text; hover help remains local and the saved/draft state line is unchanged.
- Final feedback polish keeps destroyed-row pruning on the same bounded disabled-state cleanup schedule when Chat Readability is off, applies the one-time 10-minute Sweep safety migration described above, and keeps public rescue guidance concise without changing its fail-closed transport or authority rules.
- Added a supporter and trusted Helper to the in-game and README community thanks.
- Changed Asset Sweep timing to a 10-minute safety default with a 5–180 minute range; updaters receive enabled/10-minute defaults once and later intentional choices remain untouched.
- Made mention autocomplete and player-list @ actions insert a stable whitespace-free identity token, so names decorated with rich text resolve, highlight, and trigger the configured local ping sound.
- Prevented duplicate same-client host visibility handling from leaving the host invisible locally after restoration.
- Repaired host and Helper `@lobby` alerts against build 24141704's actual chat RPC. `isLocal` is channel scope, not sender identity, and the later UI row index ambiguously falls back to zero. BlueSage now captures the authoritative `RPCInfo.sender` before that information is discarded, accepts a real unique row-zero host, rejects missing/duplicate rows, fresh-revalidates the exact Steam identity and Helper authority, and performs no roster work for ordinary chat.
- Made automatic Asset Sweep keep its real-time Unity timer advancing while On-Together is unfocused. BlueSage temporarily owns `Application.runInBackground` only while its automatic sweep loop is enabled, keeps `Resources.UnloadUnusedAssets()` on Unity's main thread, and restores the prior runtime setting on disable, compatibility stand-down, or unload.
- Reduced always-ready Rescue/Hook idle polling from every lobby member to the authoritative owner plus explicitly published Helpers. A parsed request still requires exact current-lobby membership before execution; rescue movement now updates the local movement/root transforms, clears falling velocity, and force-syncs at a bounded 250 ms Hook interval.
- Added dual exact-target Rescue/Hook addressing without weakening public moderation: `v2` uses one verified exact SteamID64, while `v3` may use only one unique active native PlayerID when Steam enumeration is still syncing. The target client must match its exact local identity; duplicate, stale, malformed, wrong-target, replayed, prior-owner, and revoked-Helper requests still fail closed.
- Replaced the source-level host/Helper player-list freeze path introduced by 0.2.0 identity decoration. Row initialization no longer launches a global highlight refresh, unflagged rows perform no identity capture, and one bounded immutable roster snapshot now feeds the player tooltip, Helper workspace, Clone Shield display, and role highlights. Join/leave invalidation refreshes on the next frame so Unity IMGUI Layout/Repaint sees one consistent model; exact moderation actions still bypass the display cache and revalidate fresh live authority.
- Replaced quadratic per-row identity uniqueness scans with one linear count/index pass per snapshot, cached reflected row fields, and added count plus last/max capture timing to the existing compact diagnostics. This removes BlueSage's N-to-the-fourth main-thread work during crowded player-list construction without weakening exact SteamID/PlayerID/transform/Steam-lobby agreement.
- Precomputed host/self/Helper/Clone-Shield roles once per roster snapshot instead of reparsing lobby Helper metadata and allocating incident snapshots for every verified row.
- Added a five-second amortized destroyed-row prune while Chat Readability remains active. Dead TMP rows now release BlueSage-owned outline materials plus retained font/backdrop/overlay/scale/window state instead of surviving until a settings change.
- Extended compact BlueSageDiag checkpoints with process working/private/managed memory and GC counts, enhanced-panel button created/destroyed/live counts, and Chat Readability retained-state counts. These counters let testing distinguish a BlueSage object-retention regression from game-owned avatar/asset churn without dumping identities.
- Corrected a reported split-brain state: a unique native PlayerID and live transform missing only from the instantaneous Steam member enumeration is now `LIVE / STEAM CHECK`, profile-openable but moderation-locked. `LEFT / STALE` is reserved for a row with no attached live native player; duplicates and other ambiguity remain `CHECKING` and fail closed.
- Made Helper/Safety player and native-ban selections explicitly reversible: click the selected row again or use `Clear Selection`. Paging, filtering, a missing row, and lobby/access changes clear the selection; BlueSage never silently auto-selects or retargets an unban action.
- Unified Status Styler preview, copy, and apply through the same bracket-aware suffix policy. Existing `()`, `[]`, `{}`, or other two-character status brackets now render identically in the preview, clipboard text, and live applied name.
- Kept a genuinely departed retained-history tooltip at `LEFT / STALE` while unrelated current identity sources sync; global roster completeness no longer relabels a known departed row as `CHECKING`, and Steam enumeration alone can no longer invent a departure.
- Clarified Custom Rich Text truth: BlueSage validates balanced tag structure and preserves exact markup, while the game remains authoritative for whether uncommon TMP tags or values render.
- Hardened the matching BlueSage PlayerLimitLift 1.1.2 host stack so above-15 remote chat preserves its authoritative RPC sender when re-broadcast. Remote slash text can no longer look like a command typed locally by the host or by another host-side handler.
- Reworked crowded-lobby identity truth after a crowded-lobby report of 60–63 native rows versus 54–57 current Steam members. Current players now verify individually while departed/history rows are labeled `LEFT / STALE` and excluded; `CHECKING` is reserved for a live source still syncing. A stale row no longer makes every valid neighbor look unverified, and the Lobby Safety banner shows verified/native/Steam/excluded counts. QoL installation is not part of verification.
- Upgraded Helper compatibility to a fresh `v3-memberdata` capability. Hosts republish it every five seconds and menus check access again automatically; the manual button is now **Refresh Access Now**. New helpers cannot mistake an older chat-transport host for compatible, legacy chat-shaped moderation/incident packets are rejected without execution, and protected requests use non-chat Steam member data.
- Added one-click **Copy Identity Evidence** plus `/sidwho copy <query>` parity, with exact revalidation before copy/profile actions. The record includes displayed and raw TMP names, cached Steam persona, exact SteamID64, verified row, PlayerID, and role; nothing is sent to chat.
- Increased the QoL footer reserve to 120px and removed duplicate long-message concatenation so wrapped Helper-waiting/status text remains visible on short viewports instead of clipping at 32px.
- Added build identity and count-only `verified/native/steam/excluded/complete` plus Lobby Safety state to BlueSageDiag so logs can distinguish old 0.2.0 binaries and explain roster drift without exposing ordinary-client SIDs.
- Updated the Thunderstore dependency pin to BepInExPack `5.4.2305`, matching the current On-Together package and Blue's tested loader core.
- Fixed a Helper discoverability failure reported by a tester: an assigned Helper now sees **Helper / Safety** while waiting for a compatible host capability, but exact IDs, incidents, and moderation actions remain locked until the host publishes a fresh compatible v3 member-data capability.
- Restored restrained Style Helper button semantics after tester review: **Open Style Helper** is neutral like nearby actions, while **Close Style Helper** becomes sage only while the helper is actually open. Coral remains selected/active, gray remains off, and orange remains experimental.
- Finished the Harbor and Midnight Dock contrast pass: neutral, selected, enabled, Off, callout, and experimental buttons now own every normal/hover/pressed/focused state with readable text polarity. Fields and focus borders, command popups, footers, section borders, previews, and Style Helper swatches now use the same theme-owned treatment; swatch labels choose light or dark text from the displayed color.
- Highlighted the local-only `HOME`, `INSERT`, and `SLASH COMMANDS` navigation keywords with the same restrained callout language in the first-lobby tips and Getting Started help.
- Made the authenticated exact-target Rescue/Hook compatibility receiver always ready in public QoL, with no normal-user toggle to discover or accidentally disable. It accepts the verified-Steam `v2` target or the unique-active-native `v3` target described above; both remain fresh, nonce-guarded, owner/Helper scoped, current-lobby-bound, and fail closed.
- Reduced the receiver's duplicate-action cooldown from ten seconds to one second so a deliberate Rescue-then-Hook workflow is usable while still suppressing same-frame repeats.
- Removed the legacy `!bsrescue` / `!bsvisible` chat-shaped receive paths. Current rescue and visibility controls use hidden Steam member data only, so control payloads cannot become public chat through BlueSage 0.2.0.
- Fixed compatible host invisibility fallback to bind to the live remote avatar transform, rescan newly spawned/re-enabled renderers on every fresh update, and restore captured renderer states on show, expiry, owner change, or despawn.
- Added `/selfmove` (`/bluemove`, `/qolmove`) parity for the supported Save Pos, Teleport Saved, Noclip/Fly, speed, jump, gravity, status, and reset menu actions. Saved and rescue teleports request the local owner's normal network transform sync after applying.

### Reconnect Recovery

- Rebuilt Reconnect Guard around the game's native `MainSceneManager.ConnectionLost` signal. Every ordinary/manual lobby join remains observational and cannot trigger cleanup.
- Native teardown gets up to 10 seconds to reach a clean lobby menu before one canonical `JoinByCode(..., false)` saved-lobby attempt; direct transport restart is not used.
- Generation ownership prevents duplicate callbacks from creating overlapping reconnect loops. Post-join validation gets up to 45 seconds and requires authoritative Steam membership, connected PurrNet, restored local player/controller/UI, and two seconds of stable quorum; Vivox is required only when lobby voice is enabled.
- Failed saved-lobby validation returns through native cleanup without retrying the same lobby. Same-host replacement discovery permits one separately validated public/direct-join replacement lobby and rejects ambiguous or incompatible matches.
- Fixed the healthy-session false-cleanup loop and the stale vanilla `IsConnecting` gate. Community testing subsequently recorded repeated successful recoveries from genuine disconnects on this release path.

### Identity, Styling, and Readability

#### Independent Clone Alerts

- Clone Shield now lets a potential victim's own QoL client detect complete avatar/outfit matches and exact letter/color/rich-text name matches, publishing one cautious global advisory with partial IDs and a request to verify the player list/profile.
- Added a bounded per-lobby Clone Shield incident lifecycle (`C001`, `C002`, …) with exact victim/suspect Steam IDs, open/resolved/locally dismissed states, closed history, and deliberate highlight cleanup. Incident commands always resolve the suspect while protecting the victim from the quick-ban path.
- Tightened Clone Shield and `VERIFIED` identity to the same current Steam-lobby/native-Steam/PlayerID quorum. Departed native history rows are excluded per row; Clone Shield still waits unless every current Steam member has one unique live row. Two consecutive absent or no-longer-matching scans archive an incident; per-player, bulk, and history cleanup remain local and never touch bans, Helpers, or lobby state.
- Added exact host/Helper structured audit records for clone open/resolve/dismiss/stale transitions and throttled repeated roster-wait diagnostics to once per state/minute. Ordinary clients still receive no extra exact-SID enrichment.
- Added host-native `/sidban` and `/sidunban`/`/unbansid` preview/confirm commands plus `/sidlist`. The host is the only native ban writer; compatible Helper requests use non-chat Steam member data, are revalidated against verified current membership, rate/replay limited, and targeted by exact `PlayerID`.
- The host can also preview and save an exact SteamID64 to the native ban list after that player has left. Off-roster entries require the full ID, a fresh confirmation, and a verified absent roster state; they persist without pretending a kick occurred. Helpers remain limited to current verified lobby targets, and `/sidunban` reverses either path.
- The native green Steam-profile tooltip keeps a compact row check for ordinary users: `LIVE`, `LEFT / STALE`, or `CHECKING`. For the active QoL host and compatible Helpers, `Steam Profile • VERIFIED` exposes the exact SteamID64, cached Steam persona, live row, PlayerID, and HOST/HELPER/VICTIM/SUSPECT details. Only the successful authorized view expands; failure text cannot overflow across adjacent rows.
- Kept the ordinary profile check at the game's compact tooltip size while resizing/wrapping the authorized host/Helper detail view so persona, exact SID, role, and navigation no longer clip over adjacent rows. Every later native tooltip deterministically restores the original panel size, body font/wrapping, and header color. Native ban storage failures now show **NOT AVAILABLE** instead of falsely claiming an empty list.
- Added an access-scoped fifth **Helper / Safety** tab for the active QoL host and assigned exact-SteamID Helpers. It contains a paged verified current-lobby roster, exact SteamID copy, Steam-profile opening, Clone Shield incident review/history, and preview-first moderation actions; Helper-list management stays host-only. `/sidlist` and `/sidwho list` are protected compact fallbacks instead of public roster dumps.
- Added a host-only paged **Native banned players** view that keeps the game's paired saved nickname and exact SteamID64 together, supports exact-ID copy, and creates a separate preview-first unban without relying on transient chat or console output.
- Unified `@lobby` and moderation delegates into one host-scoped exact-SteamID Helper list. `/helper` is the primary command; `/lobbyhelper`, `/lobbymod`, `/modhelper`, and `/moderationhelper` remain aliases, with matching-owner legacy entries migrated safely.
- Incident resolution now uses a hidden helper-to-host request plus host-owned Steam lobby state so compatible QoL clients can clear victim/suspect highlights without adding another public chat line. Public Clone Shield alerts remain visible and cautious.
- Turning Clone Shield off now clears its current-lobby incident ledger, closed history, replay state, and victim/suspect highlights immediately; peer alerts are ignored until it is enabled again.
- Repaired the 0.1.8-era style-state edge case where Detect Current or a status/Spoons-only change could capture the full rendered identity as the saved base. Existing contaminated or deterministically repairable rich-text bases migrate once with a local rollback copy; Detect Current now reads live base/status/Spoons drift together, Custom Rich Text mode preserves exact balanced markup, and Apply verifies native serialization plus visible readback before committing or reporting success. BlueSage validates structure while the game remains authoritative for uncommon TMP tag/value rendering.
- Name Styler now accepts the configured Name/ID capacity up to 4000 characters, retains closing TMP tags, preserves detected live markup for editing, and reports the draft's actual length.
- Name, status, Spoons/mood, presets, and quick status commands preserve the exact styled base name. Invalid or oversized composed names are rejected before saved configuration or live player state changes.
- Added explicit `/setname reset` and `/statuscolor reset` recovery paths without discarding the current status, Spoons, or unrelated styled-name pieces.
- Style Helper and QoL Menu now keep their content/footer reachable at smaller resolutions, reset tab scroll positions, use truthful Save/Undo state, and expose compact 12/24-hour timestamp controls.
- Repaired manual and automatic Asset Sweep invalidating BlueSage's runtime menu textures. Shared theme textures are now protected, cached, generation-checked, and released only when the plugin unloads, so no theme-cycle refresh is required.
- Snapshot Now now creates an atomic timestamped copy of the true live `BepInEx/LogOutput.log`, copies the exact snapshot folder path, and retains the latest eight snapshots instead of only flushing the older running session mirror.
- QoL Menu and Style Helper now use a deterministic centered `1320 x 720` safe fit on first open and the full safe viewport plus scrolling on smaller displays. Intentional user size and position persist across close/reopen while they remain on-screen; a resolution or display change clamps only the geometry that would otherwise hide controls.
- Added the default `BlueSage Harbor` visual system: warm cream content cards, midnight header/footer chrome, coral active actions, sage enabled states, explicit On/Off labels, grouped feature sections, and responsive one/two/three-column layouts. Existing saved themes remain available and Reset now returns to Harbor.
- Repaired Midnight Dock text fields and text areas with explicit normal, hover, focused, and active textures; removed stretched vanilla field backgrounds that produced glow streaks and made unfocused values difficult to see. Toggle cards now use consistent Comfort, Chat, Identity, Personalization, and Experimental accents.
- Name and Status editors keep the complete raw rich-text draft visible instead of truncating closing tags mid-edit. Apply/Copy still enforce the configured limit and reject crossed or incomplete TMP tags without mutating the live name.
- Corrected the one-time Spoons migration so a timestamped player opt-in is preserved; only the old timestamp-less default-on state is hidden.
- Public helper text received a release-wide plain-language pass: first-lobby tips name the Home and Insert surfaces directly, command choices wrap instead of clipping, long supporter messages stack safely, and moderation guidance consistently asks players to verify Steam profiles before acting.
- First-lobby guidance now reports the game's actual saved Push to Talk/Voice Activation choice locally, warns when Voice Activation may leave the mic live, and reminds players that the game's Pomodoro/focus timer can earn tickets and XP. Each tip retries independently until posted, while voice-state diagnostics emit only once per lobby/state transition instead of every retry.
- Added authorized `/sidincident`/`/cloneincident` typeahead for current `C###` incidents plus resolve, dismiss, and cleanup actions. Locked-on diagnostics now log only the handled slash-command name and explicitly redact every argument, preventing names, statuses, queries, and exact moderation IDs from leaking into BepInEx output.

### Public QoL Additions

- Added self-only Save Pos, Teleport Saved, default-off Noclip/Fly, and bounded speed/jump/gravity controls under `Credits / Other`. Fly always starts off and reset/unload restores normal movement.
- Added default-on Reusable Consumables for owned canteen items and chalk. It suppresses only the known item-count decrement after acquisition; shop tickets, initial purchases, clothing, bait, rods, bobbers, and unknown future item types remain vanilla. Turning it Off restores normal consumption immediately.
- Avatar Presets 4-9 and Chalkboard Persistence now ship default-on after community validation. Vanilla avatar slots 1-3 remain game-owned and untouched, and Chalkboard Persistence still stands down when Andrew's Chalky is loaded.
- Player Render Saver remains the only orange, default-off experimental feature.
- Added cached Steam-friend minimap coloring that stands down when Aquarium51 MinimapFriends is loaded, while MiniMap ID Click remains independent.
- BlueDevDebug is locked on for consistent community troubleshooting in 0.2.0. Diagnostics intentionally exclude credentials and authentication tokens.
- Added default-on, bounded local incident evidence: a structured chat/notification JSONL transcript preserving raw rich-text and plain text together, plus a running mirror of `BepInEx/LogOutput.log`. Exact roster SteamID64/persona enrichment is limited to the verified host or assigned Helpers; `/auditlog` provides status, opt-out, folder copy, and manual snapshot controls, and nothing uploads automatically.
- Added authorized live-player filtering by partial displayed name, bounded regex, partial Steam persona, 4+ SteamID digits, `_host`, and `_self`. `/sidwho` returns every verified match read-only; helper autofill accepts only one unique live result, while ban authority remains exact-ID and preview/confirm gated.
- Native BanData changes are captured into the local moderation evidence so host player-panel bans, report-panel bans, compatible native-ban tools, and BlueSage ban/unban actions retain a timestamped action/source/outcome beside the paired saved display name and SteamID64.
- Helper-to-host moderation and incident resolution now use authenticated non-chat Steam member data. Fomo can continue observing ordinary visible chat without receiving `!bsmod`, exact-SID, or incident-control payloads; the host still revalidates authorization, live identity, nonce, freshness, and target before acting.
- Matched menu and slash-command safety flows: normal players can see local audit status, snapshot, and folder-copy actions beside the audit toggle, while authorized Lobby Safety previews now expose exact-target Confirm controls for incident/live-player bans and native-record unbans.

### Build and Compatibility

- Aligned large-host guidance with PlayerLimitLift `1.1.3`; QoL remains standalone and PlayerLimitLift remains host-only.
- Added exact loaded-mod ownership for Andrew Hush, Fomo, Echo, Alpha, Chalky, and Desync. Desync pauses BlueSage's overlapping automatic Host Health repair loop, Chalky keeps the existing board-persistence stand-down, Echo is called out as an intentional clone source, and public moderation retains exact-ID authenticated host/Helper authority without importing a community banlist in 0.2.0.
- Rebased against On-Together build `24141704` / Unity `6000.0.60f1`, including the voice-era player panel and current PurrNet/Steam/Vivox surfaces.
- Added a narrow guard for uninitialized PurrNet `NetworkReflection` instances while leaving valid reflected state untouched.
- Kept always-on troubleshooting useful in crowded lobbies while suppressing repeated copies of the same player-row and reflected-surface diagnostics; routine roster snapshots now use a compact membership fingerprint instead of rewriting every Steam ID every five minutes.
- Preserved compatibility backoffs for AndrewLin Sweep/Reconnect/Chalky, Simple_QOL/ChatTweaks-style timestamp ownership, Aquarium51 MinimapFriends, and BlueSage PlayerLimitLift.

## 0.1.9 — 2026-07-11

### Hotfix: Style and Status State Repair

- Repaired the shared `/status`, `/brb`, `/afk`, `/back`, `/clearstatus`, Status Styler, Status Color, Spoons, and mood recomposition path so every suffix-only action preserves the exact styled base name.
- Added natural `/setstatus`, `/set-status`, and `/clear-status` aliases to the existing status commands.
- Status transitions now capture the base using the old exact generated status/Spoons suffixes before changing configuration, preventing the previous status or default Gradient 3 palette from being folded into the name.
- Removed generic bracket guessing: legitimate names such as `Pru (^-^)`, `Artist [OC]`, and `Helper {QA}` are no longer truncated as if their endings were BlueSage statuses.
- Repeated preset transitions preserve the original status: `/brb`, then `/afk`, then `/back` returns to the status from before preset mode instead of returning to BRB.
- Detect Current hydrates the live rich-text colors/mode while retaining exact editable markup, and paired `<noparse>...</noparse>` validation now follows TMP behavior.
- Name/status changes now follow the verified native persistence order by saving `PlayerData` before publishing `GetPlayerIdInfo()` to the current roster.
- Fixed Spoons Save/Undo to restore the previously saved label and made invalid label validation occur before count/config mutation.
- QoL Menu and Style Helper title bars now both show the active mod version.

## 0.1.8 — 2026-07-11

### Voice-Update Rebase and Community Polish

- Added orange, default-off `EXPERIMENTAL - Chalkboard Persistence`: safe local `/chalksave`, `/chalklist`, and host-only `/chalkload` using validated atomic saves and the game's native board repaint/sync path. It automatically stands down when Andrew's Chalky is installed to prevent duplicate relays.
- Added cached Steam-friend coloring to existing minimap dots without restoring expensive floating labels. It refreshes on the bounded minimap refresh and automatically stands down when Aquarium51's MinimapFriends is loaded.
- Fixed Style Helper tab layout: switching Name/Status/Templates now returns the content view to the top, and the custom Spoons label is back inside Status Styler instead of occupying every tab header.
- Fixed Detect Current resetting its editor controls to the default Gradient 3 palette. It now hydrates the live name's actual colors/style, while Status and Spoons updates preserve the current styled base name instead of recomposing from stale cached defaults.
- Added an opt-in experimental expansion of the vanilla avatar-style selector from three visible presets to nine. Slots 1-3 remain entirely game-owned—including existing player designs and unused vanilla defaults—while slots 4-9 use a separate BlueSage save bridge and apply through the game's normal customization/network path. The orange `EXPERIMENTAL` menu toggle defaults off and can remove/restore added rows without deleting either preset bank.
- Corrected host-visibility maintenance so it runs only for the local movement controller instead of once per spawned avatar per frame, and throttled active owner/roster validation to twice per second.
- Reduced idle focused-chat overhead by caching mention-ping and command-typeahead input/caret state.
- Suppressed identical successful player-row diagnostics for 30 seconds with a bounded cache while preserving new-row evidence.
- Corrected Reconnect Guard diagnostics to describe canonical saved-lobby cleanup/rejoin instead of a stale direct-`StartClient` path.
- Rebased against On-Together build `24141704` / Unity `6000.0.60f1` and refreshed the player-panel integration for the voice-era row layout while leaving native voice, moderation, Pomodoro, and game-owned controls unobstructed.
- Rebuilt Reconnect Guard around the game's canonical lobby join flow. It rejects transport-only ghost-member reconnects, requires authoritative Steam membership plus usable PurrNet/player runtime, validates Vivox alignment when voice is enabled, and uses bounded same-host public replacement-lobby discovery before safe menu fallback.
- BlueSage now suppresses AndrewLin Reconnect's competing transport-only retry while managed recovery is active instead of yielding to a path known to produce lobby-count ghosts.
- Reworked Chat Readability from overlapping periodic full-tree passes to event-driven newest-row styling plus deferred topology/config reconciliation. Unchanged TMP rows avoid repeat material/mesh dirties and destroyed UI state is pruned.
- Chat Backdrop now follows the current chat scroll-view topology with a stretch-anchored, non-raycast-blocking fallback overlay while preserving native mouse-wheel scrolling.
- Replaced the unconditional `250`-row history override with configurable `ChatHistoryRows`: default/reset `100`, range `25-250`, `/chatrows` (`/chathistory`, `/chatr`), menu control, status, and typeahead. A higher vanilla or other-mod getter result is never lowered.
- Added explicit 12-hour AM/PM and 24-hour timestamp controls plus reset/status behavior.
- Improved Name Styler rich-text detection/preservation and bounded undo/redo/reset behavior; Status/Spoons reset paths now restore their documented defaults.
- Added custom Spoons/mood labels through `/setmood` and Style Helper while keeping the public default hidden `[5/5sp]` state.
- Secured `@lobby` mass alerts: only the authoritative lobby host or host-delegated Steam-ID helpers can trigger them. Added host-only `/lobbyhelper add/remove/list/reset`; unauthorized or name-spoofed `@lobby` remains ordinary chat.
- Reworked Player Render Saver around distance bands, hysteresis, bounded work, restoration, and diagnostic counters. It remains an orange `EXPERIMENTAL`, default-off performance test until live testing proves actual renderer suppression without hiding chat/player-list identity.
- Added Main-tab community thanks for our supporters and testers; refreshed Discord/support links and added an in-menu version/last-updated stamp.
- Locked BlueDevDebug diagnostics on for public `0.1.8`, including old disabled configs. `/bluedevdebug off` truthfully explains the release lock; diagnostics intentionally exclude credentials and authentication tokens.
- Copy Chat now attaches its same sanitized, link-safe click handler to local `AddNotification` rows, making BlueSage command/system responses copyable without adding names or timestamps to the clipboard payload.
- Kept the public package focused on the documented player-facing QoL surface.

## 0.1.7 — 2026-07-06

### Hotfix

- Fixed Chat Backdrop breaking mouse-wheel chat scrolling. The backdrop now only adjusts/restores vanilla chat image color and alpha; it no longer disables the raycast targets Unity uses to route scroll input.
- Kept the `0.1.6` scroll-ownership cleanup: BlueSage still avoids rewriting vanilla `ScrollRect` movement/sensitivity while preserving chat height, font size, whole-panel scale, outlines, retained rows, and `/chatvanilla`.

## 0.1.6 — 2026-07-06

### Hotfix

- Fixed chat scrolling compatibility after the `0.1.3` chat layout work. BlueSage no longer rewrites vanilla `ScrollRect` movement, scroll sensitivity, scrollbar visibility, or viewport mask settings during chat refresh.
- Chat height, chat font size, whole-panel chat scale, outlines, backdrop, and retained rows still work, but native chat scrolling and scroll-speed mods keep ownership of actual scroll input.
- Kept `/chatvanilla` and the Customize reset button as the safe reset path for chat scale/font/height back to vanilla values.

## 0.1.5 — 2026-07-06

### Emergency Hotfix

- Fixed a `0.1.4` reconnect loop where players who had already entered a healthy lobby could be misread as stuck and forced back through join again.
- Reconnect Guard now treats visible lobby entry as success and stops interfering while the game finishes settling local player/chat runtime objects.
- Native join-by-code watchdog is now observational: it records debug breadcrumbs but does not force a saved-lobby retry or menu fallback after lobby entry succeeds.
- `/bluedevdebug` breadcrumbs are on by default so community logs include clearer reconnect and UI diagnostics. Use `/bluedevdebug off` for quieter logs.

## 0.1.4 — 2026-07-06

### Hotfix

- Reconnect Guard now requires the local player/chat runtime to finish loading before it declares reconnect success, instead of trusting transport/lobby signals alone.
- Added a guarded join-by-code loading watchdog: if a lobby join enters a stuck loading state, BlueSage retries that saved lobby once and then returns toward the menu instead of leaving the player trapped until relaunch.
- Reconnect fallback now clears the unhealthy multiplayer session before calling the game's normal menu return path, without re-triggering another guarded reconnect loop during that fallback.

## 0.1.3 — 2026-07-05

### Chat Layout Upgrade

- Added `ChatWindowHeightPercent` for players who want taller chat space without scaling text or UI. Default stays vanilla `100`; supported range is `100-200`.
- Added `/chatheight`, `/chatstretch`, and `/chath` plus `on/off/toggle`, Customize -> Chat window height, Save/Undo, typeahead suggestions, and `/qol status` reporting as `ChatWindowHeight`.
- Added `ChatFontSizePercent` for players who want larger chat message text without scaling the whole chat UI. Default stays vanilla `100`; supported range is `75-200`, with `/chatfont`, `/chatfontsize`, and `/cf`.
- Added `/chatvanilla`, `/chatreset`, Customize -> Reset Chat UI to Vanilla, and unload/reset restoration for whole chat UI scale, chat font size, and chat window height.
- Retained chat logs are now automatically raised to 250 rows for both Global and Local, so players can scroll back through longer sessions even if they leave chat height at vanilla size.
- The larger chat window uses the same verified chat panel roots as the existing scaler, with stable vertical expansion that avoids the older width-expansion path until native wrap-width behavior is proven reliable.
- Retained rows use the current `GameSettings.GlobalMessageLimitCount` and `GameSettings.LocalMessageLimitCount` getter path, raising the game limits without touching chat networking or message text.
- Height reset restores vanilla layout, while active height changes keep the current bottom edge stable so players can still use the native chat drag handle for final positioning.
- Refreshed in-game wording, README, and test notes to make the difference clear: `/chatfont` is for bigger message text only, `/chatscale` is for scaling the whole chat UI, `/chatheight` is for more vertical visible space, and deeper retained rows are automatic.
- Documented that native Global/Local chat tabs remain game-owned in this release while BlueSage improves readability, retained rows, font size, whole-panel scale, and vertical height around them.
- Reconnect Guard now waits about `5` seconds before its first managed reconnect attempt, limits itself to `3` short attempts, validates that the visible lobby/player UI becomes usable before calling reconnect successful, then tries the saved last lobby once through the game's native join-by-code flow before falling back toward the normal menu flow if the stuck void-lobby state remains.
- If AndrewLin Reconnect is installed, BlueSage now defers to that mod's reconnect loop instead of stacking competing reconnect behavior.
- Added Clone Shield as a defensive local warning when another visible roster row appears to reuse your current avatar/outfit. It ignores shared name colors, font styling, and status text to avoid false positives, warns locally only, and does not perform moderation actions.
- Added optional Player Render Saver for very large lobbies. It is off by default, local-only, hides far/overflow remote player renderers on your PC only, keeps chat/player-list rows visible, and restores renderers when turned off.
- Tuned QoL Menu and Style Helper max-safe opening size down slightly so they still auto-fit but leave more game/chat area visible on launch.

## 0.1.2 — 2026-07-04

### Feedback Polish

- Tuned final feedback defaults: Spoons remains opt-in/off by default, Ping Sound stays on the `ticket` sound, and default chat outline intensity is now `75`.
- Older enabled Spoons configs are reset to hidden once on update so the compact tag is truly opt-in again; players can turn it back on with `/spoons on`, `/spoons 0-5`, or Style Helper.
- Existing users with untouched `50` or short-lived `60` chat outline defaults migrate to the current `75` default on update while custom outline values are preserved.
- Name Styler and Status Styler now draw from the same responsive color library layout so their swatch choices match cleanly.
- QoL Menu and Style Helper now open at their largest safe on-screen size, keep a small game/chat lane visible where possible, and still let players shrink them with the bottom-right resize handle.
- Menu helper text and footer guidance are more noticeable so players can hover a control and see the exact answer without hunting through docs.
- `Home` toggles the QoL Menu, `Insert` toggles Style Helper, lobby welcome tips call out the main keys/commands, and `/back` restores the status that was active before `/brb` or `/afk` when possible.

## 0.1.1 — 2026-07-03

### Hotfix

- Restored chat readability outline behavior after the 0.1.0 release: exact true-black rich-text names/messages now trigger the configured black-text outline color again, including `<#000000>`, `<color=#000000>`, and `<color=black>` tags.
- Kept black detection precise so dark-but-not-black colors like `#000080`, `#000001`, and `#101010` still use the normal outline color instead of being forced onto the black-text outline path.
- Made the local UI outline/shadow apply its configured alpha directly so normal and black outline colors render more consistently over bright or busy backgrounds.

## 0.1.0 — 2026-07-03

### Highlights since 0.0.10

- Full Early Access QoL menu flow: `Home` or `/qolmenu` opens settings, `/styleui` opens Name Styler, Status Styler, templates, colors, and optional Spoons, and `Run QoL Status` gives a quick local feature check.
- Chat readability/accessibility is now a first-class feature: local outline/shadow, restored TMP material outline, `/chatoutline 25-100`, `/chatscale` and `/chatresize 75-200`, Customize -> whole chat UI scale, back on-screen nudging for scaled panels, `/chatoutlinecolor`, and `/blackoutlinecolor`.
- `BlackNamesOutline=on/off` defaults on so true-black styled chat/name rows can use the lighter black-text outline color instead of disappearing into dark chat backdrops.
- Longer text defaults are now `3000` for chat, Name/ID fields, ID-card details like About Me/FavoriteQuote, and room names, with cautious local tuning up to `4000`.
- Chat links are easier to read and safer to use: links get hyperlink color, plain click copies, and `Shift`-click warns before opening.

### Polish and Defaults

- Existing users get a one-time safe config migration: old untouched text limits move to `3000`, old `60`-minute sweep defaults move to `30`, untouched outline intensity moves to the current default, and Spoons moved toward opt-in instead of being assumed visible for everyone.
- Ping Mentions now default to the `ticket` sound, including migration from the old untouched `click` default.
- QoL Menu and Style Helper are cleaner: toggles use normal On/Off buttons, Customize keeps safe editable rows with Save/Undo, identity edits live in Style Helper, and Midnight Dock is the default theme.
- Status Styler and Spoons are tidier: Spoons stays compact as `[1/5sp]`, remains opt-in, and has Save/Show/Hide/Undo controls beside status edits.
- Templates apply immediately when loaded, with Undo available if the wrong slot was picked.

### Fixes

- Welcome tips retry until the local notification system accepts them, so the welcome no longer vanishes during early lobby/chat setup.
- Detect Current now prefers live in-game values, keeps the current live name style in preview/copy/apply, and only strips BlueSage status/Spoons suffixes before filling an editable draft.
- Menu/footer tooltip and status text areas are consistent across QoL Menu and Style Helper tabs, with no cross-window tooltip bleed.
- Typeahead command popups are scrollable and more readable over busy chat.
- ID-card details now follow the Name/ID text limit for all player data fields, including the bottom About Me/FavoriteQuote section, instead of the vanilla `1000`-character publish cap.
- Ping Mentions now match by base name but render the full current display label once, so `@Sam` can highlight `Sam [Tired] [4/5sp]` and `@Blue` can highlight `Blue (Homelab) [3/5sp]` without duplicating suffixes; status/Spoons text does not duplicate inside ping messages.

## 0.0.10 — 2026-07-02

### New

- Midnight Dock became the default menu theme, and the broken High Contrast theme was removed.
- Added a per-lobby welcome message so players see `Home`, `/qolmenu`, `/styleui`, `/qol status`, and typeahead tips when they join a lobby.
- Chat links now show as hyperlink-style text. Plain click still copies; `Shift`-click first copies and warns, then a second `Shift`-click opens the same link.

### Improved

- Moved Purge Unused Assets to the Main tab beside the other quick actions.
- Toggles use two columns so the menu space is easier to scan.
- Fresh configs moved the visible beta features on for testing, while existing configs kept saved player choices.
- Spoons stays compact as `[1/5sp]`; the old mood command and mood config fields were removed so names do not flood chat.

## 0.0.9 — 2026-07-02

### New

- Added menu themes: OG, Vanilla Cream OT, and Midnight Dock.
- Theme choice applies to the QoL Menu, Style Helper, and autocomplete/typeahead popups.
- Added click-to-copy chat with `/copychat` and `/ccopy`.
- Added safe chat-link opening: plain click copies text, and `Shift`-click opens the first safe web link.
- Added chat readability with `/chatreadability` and `/chatread`.
- Added BetterMove: `Shift` runs and `Ctrl` slow-walks.
- Added `/brb` and `/afk` quick status shortcuts.

### Improved

- Fixed gradient/color output so color tags close cleanly and do not bleed into later text.
- Style is now for names, while Status is for activity tags like `(Reading)`.
- Style Helper templates now save name styling only, so status does not change by accident.
- Added the color library to Status so players can pick status colors without hex-code homework.
- Spoons is now off by default for new installs, uses a compact `[1/5sp]` tag when enabled, and has `/spoons off` plus a QoL Menu hide button for shorter names.
- QoL Menu customization rows now include Save plus a back-out button so players can discard unsaved edits.
- Slash-command typeahead is scrollable when browsing commands with `/`.
- Chat readability now tries a stronger local text outline, and Chat Backdrop can restore saved backdrop colors when turned off.
- MiniMap dot-to-ID is the supported minimap helper for this release.
- Floating minimap name labels stay removed because large lobbies reported FPS/CPU lag; clicking dots for ID cards keeps the useful part without name clutter.
- Chat readability improves local text contrast without changing what other players see.
- BetterMove only affects your own character locally.
- README now keeps the Thunderstore-facing notes shorter and more player-friendly.

## 0.0.8 — 2026-06-29

- Added the visible `@name` suggestion popup for Ping Mentions.
- Made Style Helper and QoL Menu open larger by default with easier resize handles.
- Expanded the Style Helper color library.
- Retired the broken tiny Easter egg buttons and kept one compact optional one-time lobby joke with truthful labeling.
- Player-list helper buttons now protect native report/ignore/kick/ban controls first.
- Enhanced Player Panel helper logs now stay separate from host-health logs.
- README now lists both the official On-Together Discord and Blue's Discord Community.

## 0.0.7 — 2026-06-28

- Fixed Ping Mentions so typing `@name` and pressing `Tab` works naturally.
- Kept `[PING]`, `@lobby`, and selectable sound modes.
- Reconnect Guard now tries `20` times about every `15` seconds, then returns toward the normal menu/error flow.
- MiniMap dot-to-ID remains on by default.
- Floating minimap labels are removed after large-lobby FPS/CPU lag reports.
- Style Helper and QoL Menu now have resize grips, screen clamping, vertical scrolling, and clearer tips.
- Home now opens/closes `/qolmenu`.
- Player-list helper buttons sit left of the native/Pomodoro/PomoSync action area so report/ban buttons remain reachable.

## 0.0.6 — 2026-06-28

- Fixed the v0.0.5 player-list button layout regression.
- Style Helper and QoL Menu became easier to read and scroll.
- Added Discord buttons to the Style Helper and QoL Menu headers.
- Moved beta controls into Toggles and changed the old Beta tab into **Credits / Other**.
- Added `Reset Current` to the Style Helper.
- Ping Mentions got yellow completed mentions, visible `[PING]` marking, `@lobby`, and selectable sound modes.
- MiniMap click-to-ID-card stayed as the useful behavior while floating labels were removed.
- Credits / Other invites players to screenshot/share finds in Blue's Discord.

## 0.0.5 — 2026-06-28

- Chat, ID/profile fields, room names, and style-generated text now default to 1500 characters.
- Text limits are user-expandable up to 4000 through config or `/qolmenu`.
- Host Health now defaults off and is available through manual `/hh run`.
- Added safer player-list row refresh for larger lobbies and recycled player-list rows.
- Added Pomodoro/PomoSync spacing protection for player-list helper buttons.
- Added MiniMap dot-to-ID support.
- Added `/styleui` and `/qolmenu`.
- Added PlayerLimitLift companion awareness for Host Health.
- Fixed timestamps so they can apply to chat plus join/leave/system notices.

## 0.0.4 — 2026-06-28

- Reduced false Host Health warnings and improved Enhanced Player Panel behavior.
- Added useful mention pings, the first Style Helper window, and safer UI readiness checks.

## 0.0.3 — 2026-06-18

- Early beta cleanup.
- Added `/style` / `/sty`.
- Added Focus Anywhere.
- Added Enhanced Player Panel helpers.
- Added optional PlayerLimitLift companion awareness.
- Reduced Host Health false-positive noise.

## 0.0.2 — 2026-06-18

- Rebuilt as `0.0.2` so Thunderstore could accept the package after the deprecated `0.0.1` upload.

## 0.0.1 — 2026-06-18

- First Early Access release of BlueSage QoL Tweaks - Beta.
- Added Asset Sweep, Host Health, longer chat/name/room text, leave notices, optional timestamps, Reconnect Guard, and internal `/as`, `/hh`, `/ln`, `/tt`, `/rg`, `/rm`, `/bsqol`, and `/qol` commands.
