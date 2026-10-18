# BlueSage On-Together Mods

Source for the two BlueSage mods on Thunderstore for **On-Together: Virtual Co-Working**:

| Mod | What it is | Who installs it |
| --- | --- | --- |
| [BlueSage QoL Tweaks](BlueSage_QoL_Tweaks_Beta/README.md) | Readable chat, name and status styling, help for tired hands, Work / Relax tracking, and gentle host tools | Anyone who wants them |
| [BlueSage PlayerLimitLift](BlueSagePatched_PlayerLimitLift/README.md) | Bigger lobbies: up to 128 joinable players plus the host | The host only |

Just want to play? Grab them from Thunderstore with Gale, r2modman, or the Thunderstore app. Everything below is for people who want to read or build the code.

## What's in here

- `BlueSage_QoL_Tweaks_Beta/`: the QoL mod (`src/`), its tests (`BlueSage_QoL_Tweaks_Beta.Tests/`), and its release checker (`tools/Verify-BlueSageQoLTweaks.ps1`).
- `BlueSagePatched_PlayerLimitLift/`: the PlayerLimitLift mod (`src/`) and its checks (`tools/`).
- Each mod folder also holds the exact `README.md`, `CHANGELOG.md`, `manifest.json`, and `icon.png` that go to Thunderstore.

## Building

You need the .NET SDK, PowerShell 7, and On-Together with BepInEx installed (the mods compile against the game's own files).

1. Point the build at your game and BepInEx if they aren't in the default spots:

   ```powershell
   dotnet build BlueSage_QoL_Tweaks_Beta/BlueSage_QoL_Tweaks_Beta.csproj -c Release `
     -p:GameRoot="D:\Steam\steamapps\common\On-Together\On-Together" `
     -p:BepInExProfileDir="$env:APPDATA\com.kesomannen.gale\on-together\profiles\<your profile>\BepInEx"
   ```

2. Run the QoL tests (these don't need the game):

   ```powershell
   dotnet tool restore
   dotnet test BlueSage_QoL_Tweaks_Beta/BlueSage_QoL_Tweaks_Beta.Tests
   ```

3. The release checkers (`tools/Verify-*.ps1`) build the package zip and check it against the live game files, so they need the game installed.

The default paths in the project files are the ones on our own machine. Change them with `-p:` as above instead of editing the files.

## Help and community

- Community Discord: https://discord.gg/JujMEwtN3q
- Website: https://bluesagetech.net/ (Coming Soon)

## License and credits

BlueSage's own code is released under the GPL-3.0 license (see `LICENSE`). PlayerLimitLift began as a community patch of officer_balls' original PlayerLimitLift, which was published without a license; `NOTICE.md` explains what that means and how officer_balls can reach us. QoL is its own code, inspired by community mods from AndrewLin, Tiny_Plume, jaide, OnToWelcomeCommittee, and officer_balls. Thank you all ♥

Made by Blue + Sage = BlueSage with superpowered neurodivergence ♥
