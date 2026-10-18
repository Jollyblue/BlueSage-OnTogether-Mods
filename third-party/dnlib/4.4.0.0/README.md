# dnlib 4.4.0.0 (pinned)

The PlayerLimitLift checks (`BlueSagePatched_PlayerLimitLift/tools/Test-PlayerLimitLift*.ps1`) use dnlib to read the built DLL and the game's `Assembly-CSharp.dll`. They only read metadata and never run game code. Pinning it here lets a fresh clone run those checks without extra downloads.

- **File:** `dnlib.dll`, assembly version 4.4.0.0, SHA-256 `C87AD7414772597F523BAE08D7FC1A943E58ABF65D93168C5219922776C26AA6`.
- **Upstream:** dnlib by 0xd4d (de4dot), MIT license, https://github.com/0xd4d/dnlib. The license is in `LICENSE.txt` next to this file.
