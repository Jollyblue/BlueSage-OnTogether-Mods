# BlueSage QoL Tweaks

**Little comfort tools for On-Together, made by a pair of AuDHD IT goblins who wanted our favorite cozy game to feel even cozier.** Readable chat, name and status styling, help for tired hands, and gentle host tools. Every feature has its own switch, so keep what you like and skip the rest.

[![Version](https://img.shields.io/badge/version-0.2.7-3b82f6)](#whats-new)
[![Game build](https://img.shields.io/badge/On--Together-build%2025966942-87a96b)](#compatibility)
[![Mod loader](https://img.shields.io/badge/BepInEx-5.4.2305-6b7280)](#compatibility)
[![Discord](https://img.shields.io/badge/Discord-join%20us-3b82f6?logo=discord&logoColor=white)](https://discord.gg/JujMEwtN3q)
![Website](https://img.shields.io/badge/Website-Coming%20Soon-6b7280)

> **New here?** Install with Gale, r2modman, or the Thunderstore app. The mod loader comes along on its own. Press `Home` in a lobby to open the QoL menu.

[What's new](#whats-new) · [Quick Start](#quick-start) · [Work / Relax](#work--relax) · [What you get](#feature-guide) · [Settings](#settings) · [FAQ](#friendly-faq) · [Support](#troubleshooting-and-support)

**Version 0.2.7 - October 25, 2026.**

## What's new

**New in 0.2.7:**

- **Hover `[U]` to see a player's QoL version**, like `QoL v0.2.7 User`. Players on 0.2.6 or older show as `Pre-indicator QoL User`.
- **No more reconnect loops.** If a lobby is gone, Reconnect Guard stops after 5 tries in a row, tells you, and leaves you on the menu.
- **BlueSage Report shows the game's own faces and badges** straight from the QoL menu. Work / Relax history saves every five minutes, the log repeats itself far less, and the automatic cleanup no longer pops a note.
- **Hosts:** one note if Steam drops your lobby; the host journal no longer fills up with the 15-minute check.
- **See local chat and bubbles from 15 m away,** not just 5 m (QoL menu, 5 to 25 m).
- **More of what you do answers with a small toast,** and your **Extra Outfit Slots 4-9** are easier to find in the avatar screen.

**Also recent (0.2.5 to 0.2.6):**

- With Performance Overlay on, choose **WORK** or **RELAX** beside FPS / PING / RAM; commands are optional shortcuts. The game's statistics stay unchanged.
- **BlueSage Report** splits your time between Work and Relax with the Journal's five faces, and you can correct session history whenever your choice was wrong.
- Our website, https://bluesagetech.net/ (Coming Soon), gets a **WEBSITE** button in the QoL menu. Until it opens, the Discord has everything.

Hosts also get live-checked Ban buttons, **kick**, **lobby-wide mute** and a **host journal**, and extra outfit slots (4 to 9) keep your work. The full story of every release lives in the Changelog tab.

## Quick Start

1. Install **BlueSage QoL Tweaks** with Gale, r2modman, or the Thunderstore app. BepInEx comes along automatically.
2. Turn on **Performance Overlay**; choose **WORK** for productivity or **RELAX** for rest, social time, or away time.
3. Use On-Together normally. The game's own statistics keep working.
4. Open **BlueSage Report** from Report or the QoL menu to review your split.

Press `Home` for the QoL menu (or click **QoL Menu** above the stats bar). Press `Insert` for name and status styling (or click **Style Helper**). `End` toggles fly; `F8` cleans up now.

> Tip: type `/` in chat to browse every command. Tab completes commands, names, and choices.

## Your first lobby

Start with the defaults. No setup needed.

1. Join a lobby, then press `Home` when you want to explore comfort, chat, accessibility, or host tools.
2. Change one setting at a time, play for a few minutes, and keep what feels good.
3. In a busy lobby, open the player list or minimap when you need it. The first open after someone joins can take a moment while BlueSage matches the current players.
4. Want a larger room? PlayerLimitLift is host-only; joining players skip it.

## Work / Relax

While you play, BlueSage labels the game's own Report totals with the mode you picked. Totals from before, or that it can't place, stay **Unclassified**.

These are private local labels; they never change rewards, streaks, achievements, tasks, moods, or the game's save. The report can move or undo an attributed session.

- **Focus Pulse** shows your saved totals.
- **BlueSage Report** shows today and recent sessions, split into Work, Relax, and Unclassified, with the Journal's five faces in plain words.
- Switch modes from the overlay buttons, the QoL menu, or `/workon`, `/relaxon`, and `/mode`.

## Feature guide

Every feature below has its own switch in the QoL menu (`Home`). Most start on; the few that start off say so.

### Chat and readability

- Readable chat has timestamps, mentions, copy, safer links, and `/chatvanilla`.
- **Chat Readability** adds a soft outline and shadow so white or colored messages stay readable on bright scenes. True-black styled names get their own lighter outline so they never vanish.
- **Chat size, three ways:** `/chatfont` makes message text bigger, `/chatscale` scales the whole chat panel, and `/chatheight` gives the chat window more height without scaling text. `/chatvanilla` puts all three back.
- Chat keeps up to 150 recent rows by default. BlueSage never lowers a larger amount already chosen by the game or another mod.
- **Timestamps** in 12-hour or 24-hour time, on chat and on system notices, with your choice of size and color.
- **Mentions:** type `@` and a name to get suggestions. Messages that mention you are highlighted and play a soft ping sound you can change or mute.
- **Copy and links:** Ctrl-click a message to copy its text. Links are highlighted; Shift-click copies a link and warns first, and a second Shift-click opens it. A plain click still opens the game's own ID card.
- **Leave notices** tell you quietly when someone leaves.

### Your name, status, and style

- Style Helper styles names, status, and Spoons tag. Open it with `Insert`, the **Style Helper** button above the stats bar, or `/styleui`.
- **Name Styler** builds gradients and colors from a color library, keeps your existing rich text, and lets you undo, reset, or save templates.
- **Status** adds a short activity tag such as `(Reading)` after your name. `/brb` and `/afk` set quick statuses, and `/back` brings back the one you had before.
- **Spoons** is an optional social-battery tag, like `[3/5sp]`. It starts off; turn it on with `/spoons on` and rename the label with `/setmood`.
- **Extra outfit slots (Extra Outfit Slots 4-9)** are on from the start. Open the avatar screen where you change your look and find the style list: styles 4 to 9 sit right below the game's three. Pick one, change your look, name it, and it keeps that outfit. Styles 1 to 3 never change, and if you switch the extras off, your saved outfits wait for you.
- **Menu themes:** Midnight Dock (default), BlueSage Harbor, Vanilla Cream OT, and OG.

### Comfort and movement

- **BetterMove:** hold Shift to run or Ctrl to slow-walk. Only your own character changes.
- **Fly (Noclip):** press `End`. WASD to move, Space or E up, Ctrl or Q down, Shift for speed, hold Alt for slow and careful moves, and hold the middle mouse button to turn the camera. Fly always starts off when the game loads.
- **Save Pos and Teleport Saved** jump you back to a spot you like, and speed, jump, and gravity sliders tune your own movement. Everything resets with one button.
- **Extended Camera Zoom** lets scroll zoom reach much closer and much farther than normal.
- **Focus Anywhere** lets you start focus mode from most places, not only the usual focus spots.
- **Reusable Consumables:** once you own a canteen item, chalk color, or fishing bait, using it no longer lowers its count. Clothing, rods, and bobbers stay normal.
- **Toasts** are small pop-up notes for hotkeys, settings, cleanups, reconnects, and host news.

### Accessibility

- **Fishing Aid** (off by default) is for sore or unsteady hands. Take out your rod near water, stand still, and it looks for water all around you, casts, answers bites, plays the reel game, lets go of each catch, and casts again until you put the rod away.
- Menu scaling runs from 75% to 200% without changing chat size. Auto picks a comfortable size for your screen.
- Toast size and how long toasts stay on screen are both adjustable.
- Longer text: chat, names, ID-card fields, and room names accept up to 4000 characters.

### Players, safety, and lobbies

- `[U]` marks a confirmed BlueSage QoL player. Hover it to see their version, like `QoL v0.2.7 User`; players on 0.2.6 or older show as `Pre-indicator QoL User`. It stays visible in narrow player lists; it is friendly info and never grants host or moderation powers.
- **Player list helpers** add `@` (mention) and `ID` (open ID card) buttons beside each player, without covering the game's own buttons.
- **Minimap:** click a player's dot to open their ID card. Your Steam friends show up green.
- Clone alerts point out look-alike names for a host to review. They are warnings, not proof, and can be cleared.
- **Community Safety List** checks the public community safety list on your computer and adds only missing entries to your own block list. It never kicks anyone.
- A host or approved Helper can privately assist one confirmed QoL player. If BlueSage is unsure who the request belongs to, nothing happens.
- **Reconnect Guard** waits 20 seconds after a real disconnect, then rejoins once, so the host can clear your old connection first. If the same lobby drops you five times in a row, it stops and tells you, so a lobby that is gone can never hold you in a loop.
- **Another QoL mod loaded?** If TogetherPlus or Alpha is running too, one local note appears once per session so you know to try one at a time if something acts strange.
- **Chalkboards:** save boards on your computer, and let the host restore a saved board for the lobby.

### Hosting

- **Host Tools** explains each status and next step; technical details stay hidden.
- **Lobby welcome (MOTD):** QoL players see your welcome as a toast and a chat line when they join. Set it in **Host Tools** or with `/motd`.
- **Announcements:** `/announce` sends a heads-up toast to everyone with QoL, for restarts or breaks. Approved Helpers can use it too.
- **Helpers:** the host can name trusted Helpers with `/helper`. Helpers can ask the host to ban, kick or mute, and the host checks every request first. A Helper who dropped out of Steam's lobby list after an automatic reconnect can still act: the request travels over the game connection and the host checks it against that Helper's live connection, with the journal noting it.
- **Host journal:** Host Tools lists your actions (yours, a Helper's, or the game's own Ban buttons) one per line with the player's name, the RECONNECTED / LOADING label and ok or refused; hover for the Steam ID and reason, click to copy, or copy the last 20. One pinned line shows the lobby picture now; Status and Refused-only views are a click away, so a 100-slot lobby never buries a ban. The same rows go to `BepInEx/BlueSageQoLModSuite/trackers/host-journal.jsonl` on your computer, always on while you host; full pages are zipped into the `archive` folder beside it.
- **Kick** (`/sidkick`, or the row buttons in Host Tools) is a short ban that lifts itself once the player is gone, usually within 20 seconds. If they are still here after that, the ban stays and you get a note. **Lobby-wide mute** (`/sidmute`, `/sidunmute`) stops you relaying that player's chat to everyone for this hosting session; they will notice their own lines stop, and voice is unaffected.
- The player list refreshes its labels every 15 minutes while you host. That never moderates anyone on its own.
- **In-game large-lobby chat** supports bigger rooms. It is not a Discord bridge and never sends public users' chat to Discord.
- **Host Health** runs quiet background checks for long-running lobbies; `/hh run` checks right now. If Steam drops your lobby, you get one note so you can re-host.

PlayerLimitLift is a separate package: the host installs the version Thunderstore marks current.

### Performance and cleanup

- **Performance Overlay** is a one-line FPS / PING / RAM bar with WORK and RELAX. Drag it anywhere. Hosts see `PING --`, because a host has no ping to itself.
- Automatic Cleanup starts on and runs every 40 minutes, and it waits for a quiet moment: never right before or after the game's own 3-minute autosave or its 30-minute cleanup. You can turn it off or choose 5–180 minutes. It leaves the game's own cleanup alone. F8, the QoL menu, `/sweep now`, `/assetsweep now`, and `/as now` remain available whenever you want to run it yourself. The automatic run stays quiet; a cleanup you start yourself confirms in chat.

## Keys

All keys can be changed in the QoL menu's Hotkeys section or with `/rebind`.

| Key | What it does |
| --- | --- |
| `Home` | Open or close the QoL menu |
| `Insert` | Open or close Style Helper |
| `End` | Turn fly on or off |
| `F8` | Clean up now |
| `Shift` / `Ctrl` | Run / slow-walk (BetterMove) |
| `Alt` (while flying) | Slow, careful fly moves |
| Middle mouse (while flying) | Turn the camera |
| `Tab` (in chat) | Complete a command, name, or choice |

## Commands

You never need these; the menus do it all. They're here for keyboard lovers. Type `/` in chat to browse them.

**Work / Relax**

- `/workon` or `/relaxoff`: choose Work.
- `/relaxon` or `/workoff`: choose Relax.
- `/mode work` or `/mode relax`: choose a mode directly.

**Menus and help**

- `/qolmenu` (`/qm`): open the QoL menu.
- `/styleui` (`/sui`): open Style Helper.
- `/qol status`: show a short health check.
- `/bsqol`, `/guide`, or `/faq`: open the cozy guide pop-up.
- `/welcome` (`/howto`): reopen the welcome overlay.
- `/rebind` (`/keys`, `/bind`, `/kb`): view or change hotkeys.

**Chat**

- `/chatfont` (`/cf`), `/chatscale` (`/cr`), `/chatheight` (`/chath`): message size, whole-panel size (75–200%), and window height (100–200%).
- `/chatrows` (`/chatr`): how many chat rows to keep, 25 to 250 (default 150).
- `/chatvanilla` (`/chatreset`): put chat size and height back to normal.
- `/chatreadability`, `/chatbackdrop`, `/chatoutline`, `/chatoutlinecolor`, `/blackoutlinecolor`: readability outline, backdrop, strength, and colors.
- `/timestamps` (`/tt`), `/leavenotices` (`/ln`): timestamps and leave notices on or off.
- `/copychat` (`/ccopy`), `/chatlinks` (`/clinks`): message copy and safe links on or off.
- `/setpingcolor`, `/pingsound`: mention highlight color and ping sound.

**Name and status**

- `/setname`: set your base display name. `/setname reset` recovers it.
- `/status <text>` (`/setstatus`), `/clearstatus`: set or clear your status.
- `/brb`, `/afk`, `/back`: quick statuses, and back to the one you had.
- `/statuscolor`: status color. `/statuscolor reset` restores the default.
- `/spoons on|off|0-5`, `/setmood <label>`: Spoons tag and its label.
- `/style` (`/sty`): build styled text you can paste.

**Comfort and movement**

- `/selfmove` (`/qolmove`): status, save, teleport, fly, speed, jump, gravity, or reset.
- `/bettermove` (`/bm`), `/focusanywhere` (`/fa`), `/unlimitedconsumables` (`/consumables`): turn those features on or off.
- `/fishaid on|off|status`: accessibility Fishing Aid (off by default); take out your rod (H) near water and it fishes for you.
- `/minimaplabels` (`/mml`): minimap dot clicks on or off.

**Cleanup and connection**

- `/assetsweep now` (`/sweep now`, `/as now`): clean up now. `/assetsweep on|off` sets the automatic timer.
- `/reconnectguard` (`/rg`), `/reconnectmessage` (`/rm`): Reconnect Guard and its success note.
- `/hosthealth` (`/hh`): Host Health on or off; `/hh run` checks now.

**Hosting and safety**

- `/motd <text>`: hosts set the welcome QoL players see when they join (default "Welcome to my lobby!"). The same box lives in **Host Tools**.
- `/announce <message>`: host or Helper heads-up toast for everyone with QoL.
- `/helper add|remove|list|reset`: hosts manage trusted Helpers.
- `/idsync`: player-list match status; hosts and Helpers can repair broken matches.
- `/cloneshield` (`/cloneguard`): clone alerts on or off. `/sidincident` reviews or clears alerts.
- `/sidwho`, `/sidlist`, `/sidban`, `/sidunban`, `/sidkick`, `/sidmute`, `/sidunmute`: host and Helper player lookup, ban, kick and lobby-wide mute tools. Every action shows a preview and needs a confirm.
- `/communitybans` (`/bansync`): Community Safety List status or refresh.
- `/chalk` (`/chalkboard`), `/chalksave`, `/chalklist`, `/chalkload`: save, list, and (for the host) restore chalkboards.
- `/auditlog on`: turn on the optional local chat and game-log copy (off by default). `/auditlog` also takes status, snapshot, folder, or off.

## With or without QoL?

| What | You have QoL | They don't have QoL |
| --- | --- | --- |
| Your styled name and status | Shows as you styled it | Shows the same, because it is part of your name |
| Spoons tag | Shows | Shows, as part of your name |
| Readable chat, timestamps, chat size | On your screen only | Unchanged |
| Welcome pop-up, toasts, host MOTD | Yes | Not shown |
| `[U]` marker | You see it on other QoL players | They are just normal players to you |
| Large lobbies | Needs PlayerLimitLift on the host only | Joiners install nothing |

## Settings

Everything can be changed in the QoL menu; no config files needed. Mod managers also show these settings under `BlueSage QoL Tweaks`.

| Setting | Default | What it does |
| --- | --- | --- |
| Automatic Cleanup | On, every 40 min | Light memory cleanup, 5–180 minutes, never during an autosave |
| Performance Overlay | On | FPS / PING / RAM bar with WORK and RELAX |
| Community Safety List | On | Adds missing community entries to your own block list |
| Toast Pop-ups | On, 3.5 s | Small pop-up notes; 2–15 seconds; size Auto or 75–200% |
| Welcome Overlay | On | Short how-to each time you join |
| Host Announcements | On | Shows host and Helper `/announce` toasts |
| Chat Readability | On, strength 75 | Outline and shadow on chat text |
| Chat rows | 150 | 25–250 rows kept |
| Local chat range | 15 m | 5–25 m; how far away a local chat line can be and still show for you (5 m is the game's own) |
| Timestamps | On, 12-hour | Chat and notice timestamps |
| Mentions and ping sound | On, "ticket" | Mention highlight and sound |
| Menu scale | Auto | 75–200% for menus and tooltips |
| Theme | Midnight Dock | Also Harbor, Vanilla Cream OT, OG |
| Spoons | Off | Optional social-battery tag |
| BetterMove | On | Shift runs, Ctrl slow-walks |
| Extended Camera Zoom | On | Wider scroll-zoom range |
| Reusable Consumables | On | Owned consumables don't run out |
| Fishing Aid | Off | Reaction 0.7 s, 7 clicks per second, release 3.5 s |
| Reconnect Guard | On, 20 s wait | Rejoins once; stops after 5 in a row on the same lobby; wait 10–120 seconds |
| Clone alerts | On | Cautious look-alike warnings |
| Chalkboard saves | On | Steps aside when Chalky is installed |
| Text limits | 4000 | Chat, names, ID cards, room names |

## Friendly FAQ

**Where are my extra outfit slots?** In the avatar screen where you change your look, open the style list: the game's three styles come first, and BlueSage adds styles 4 to 9 right below them. They are on from the start. Not there? Open the QoL menu, go to Toggles, and check that **Extra Outfit Slots 4-9** is On.

**Do I need to learn commands first?** No. Start with the defaults and use the menus. Commands are only shortcuts when you want them.

**Does everyone need QoL?** No. It adds local comfort tools for whoever installs it.

**How do I find a command?** Type `/` in chat to browse; Tab completes it.

**Why does reconnect wait before rejoining?** So the host can clear your old connection first. Rejoining instantly can make the lobby's player count wrong or leave the lobby hard to join. The wait is 20 seconds by default; change it from 10 to 120 under **Reconnect wait** in the QoL menu's Settings. If you join somewhere else first, the automatic rejoin cancels. If the same lobby drops you five times in a row, the guard stops, shows a note, and leaves you on the menu; two minutes of normal play after any rejoin, or joining a different lobby, gives it a fresh count.

**Can I change the hotkeys?** Yes, no config file needed. Use the menu's Hotkeys section or `/rebind` (also `/keys`): `/rebind fly` then press a key, `/rebind sweep F6`, or `/rebind reset` for `Home`, `Insert`, `End`, `F8`. Keys that type into chat are refused.

**What was that pop-up when I joined?** A quick how-to shown every time you join a lobby. **Don't show this again** turns it off, `/welcome` reopens it, and its **Open the full guide** button (or `/faq`) explains it all. Turn off **Welcome Overlay** in Comfort & Movement; the chat welcome is separate.

**What are the small toasts?** Brief pop-up notes for hotkeys, settings you change (in the menu or with a command), cleanups you start, reconnects, and host news, easy to spot on a small screen. The note for the game's own cleanup is optional: turn on **Sweep Chat Note** in the QoL menu. Choose how long they stay (2–15 seconds) and how big they are in Customize > General. Turn off **Toast Pop-ups** or **Host Announcements** in Toggles (Comfort & Movement).

**How does Fishing Aid work?** Turn it on in Toggles > Accessibility or with `/fishaid`. Take out your rod with H near water and stand still: it looks for water all around you, casts, answers bites, plays the reel game, lets go of each catch after a moment, and casts again until you put the rod away. Cast once by hand to teach it your favorite spot. Its timing settings sit right under the switch; the defaults suit most people. It never buys anything, and with Reusable Consumables on your bait doesn't run out.

**How do I fly comfortably?** Hold Alt for fine, slow moves and hold the middle mouse button to turn the camera. Change both in the menu's Hotkeys section.

**Can I zoom out farther?** Yes. Scroll zoom reaches much closer and farther than normal, walking or flying. Turn off **Extended Camera Zoom** in Toggles to go back to the normal range.

**Why won't an ID card open for some players?** The game lets each player report their own Steam ID, so BlueSage confirms it first. The host checks it against Steam's own connection record and shares the result with Helpers. Other players can only confirm people listed in the Steam lobby, so a few may stay locked; the host or a Helper can still check them.

**What does `[U]` mean?** That player runs BlueSage QoL too. Hover it to see which version (`QoL v0.2.7 User`); anyone on 0.2.6 or older shows as `Pre-indicator QoL User`, because those versions never said which one they were. It stays visible in a narrow player-list row, and it never gives anyone host or moderation powers.

**Chat looks odd after I changed something.** Type `/chatvanilla` to restore the normal chat layout, then turn features back on one setting at a time.

**Host Tools says “Checking this lobby...” What should I do?** Wait for it to finish. If a name or button still looks wrong, use **1. Refresh Player List**, then a host can try **2. Recheck Player Matches**. One wrong person: ask them to rejoin. Several wrong: recreate the lobby. These buttons only repair BlueSage's player list — never kick, mute, report, or ban anyone.

**What does “Check game moderation buttons” do?** A read-only check for Mute, Ignore, Report, and Host Remove. It never acts on a player.

**My name got long or messy after styling.** Use `/spoons off` to hide the Spoons tag, `/clearstatus` to drop the status, or **Detect Current** in Style Helper to re-read your live name before trying again.

**Player list buttons look stale in a huge lobby.** Close and reopen the player list; BlueSage refreshes its buttons for the rows you can see.

**How do I get help?** Bring your game version and what you were doing to the community Discord. Read any log first and remove anything private.

## Compatibility

Windows is tested. macOS and Linux can load the mod, but still need real-world player testing.

- **PlayerLimitLift** (BlueSage PlayerLimitLift) is optional and host-only. QoL works fine without it; with it, Host Health shows the lobby limit.
- BlueSage steps aside when a mod already does the same job: automatic cleanup pauses for AndrewLin Sweep, chalkboard saves pause for Chalky, timestamps and leave notices pause for Simple_QOL or ChatTweaks-style mods, and minimap friend colors pause for MinimapFriends.

## Privacy

Visible labels never grant authority. Community Safety and the Performance Overlay start on; Spoons start off. Any optional local audit stays on your computer until you choose to share it; read it first and remove anything private.

BlueSage keeps its own files in one folder, `BepInEx/BlueSageQoLModSuite` (a `README.txt` inside explains each part). Gale, r2modman and the Thunderstore app each give every profile its own `BepInEx` folder, so each profile keeps its own small BlueSage folder and they never mix. Older files are zipped and kept 14 days, and the folder never stays over 1 GB. Removing the mod or the profile removes it too.

- Nothing is uploaded automatically.
- Styling, chat size, and readability change only your own screen.
- Host and Helper tools check every request before anything happens.

## Troubleshooting and Support

Community Discord: https://discord.gg/JujMEwtN3q

1. Run `/qol status` for a quick health check.
2. Open the QoL menu and turn off only the feature that feels wrong. `/chatvanilla` resets chat; restart after changing the lobby size; or roll back in your mod manager.
3. Still stuck? Bring your game version and what you were doing to the Discord. If you share `BepInEx/LogOutput.log`, read it first.

Website: https://bluesagetech.net/ (Coming Soon)

## Credits and thanks

Big love to the modders whose work inspired ours:

- AndrewLin: Sweep, Reconnect, Fomo, Echo, Alpha, and Chalky
- Tiny_Plume: Simple_QOL
- jaide: EnhancedPlayerPanel, CopyChat, and PingSystem
- OnToWelcomeCommittee: FocusAnywhere
- officer_balls: PlayerLimitLift and StatusManager
- On-Together Name Customiser
- OnTogetherModTemplate

And to every supporter and tester who stuck with us through the bugs: thank you. You're named in the QoL menu's Credits.

Made by Blue + Sage = BlueSage with superpowered neurodivergence ♥
