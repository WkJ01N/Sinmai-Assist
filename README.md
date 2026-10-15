# Sinmai-Assist

A MelonLoader mod for maimai DX (`Sinmai.exe`), with gameplay tools, practice controls, automation, and compatibility fixes.

Original authors: **WYH2004 (Slim) & Error063** | Fork maintainer: **WkJ01N**

Use **MelonLoader 0.6.4 or earlier**. Current compatibility checks target **SDGB 25500 (SDGB156E)**; other versions require verification.

## Getting Started

1. Install MelonLoader and place the built `Sinmai-Assist.dll` in the game's `Mods` folder.
2. Launch once, edit `Sinmai-Assist/Config.yml` to enable the required modules, then restart.
3. Use the in-game panels to adjust settings. If the panel is hidden and has no shortcut, click **Open Sinmai-Assist**.
4. Click **Save settings, presets and shortcuts** to persist changes. Startup restores the last manually saved settings.

## Features

### Gameplay and Practice

- **Auto Play** - Play automatically with fixed judgements or the original random AP, FC+, and FC modes.
- **Rank-controlled Auto Play** - Target a rank and minimum combo flag, including takeover during a song while preserving earlier judgements.
- **Fast Skip** - End the current song through the native result flow, with optional custom achievement and forced Miss settings.
- **Random Rank Skip** - Generate full-chart judgements within a chosen rank interval and minimum Any, FC, FC+, AP, or AP+ requirement.
- **Batch Score Farming** - Automatically process a frozen queue from the selected song, with song/category limits, current/all difficulties, and bounded retries.
- **Low-level Boost** - Optionally sample higher rank/combo targets using editable weights for each displayed chart level.
- **Chunked Uploads** - Save native CN play logs in batches, clearing confirmed logs and resetting track numbers only after successful acknowledgements.
- **Batch Recovery** - Record progress and upload receipts locally for manual continuation with the same players after review.
- **Chart Controller** - Pause, seek, record a position, return to it, or restart the song.
- **A/B Loop Practice** - Repeat a section with fresh judgements and combo each round, separate loop counters, and a configurable rest interval (default 5 seconds).
- **Practice Speed and Pitch** - Practice at 0.50x-2.00x independently of SpeedHack, with optional pitch preservation; changes apply next round.
- **Speed Hack** - Adjust gameplay speed and optionally apply global speed to other game scenes.
- **All Collections** - Unlock collectible items for the current user.
- **Unlock Music** - Make locked songs available, with an optional user-data save setting.
- **Unlock Master** - Unlock MASTER and Re:MASTER charts, with an optional user-data save setting.
- **Unlock Event** - Enable otherwise locked events.
- **Unlock Utage** - Enable Utage content, including an option to unlock charts intended for two players.
- **Force Current as Best** - Replace the stored best with the latest result, potentially changing rating records.
- **Reset Login Bonus Record** - Clear the user's recorded login bonus history.
- **Rewrite Login Bonus Stamp** - Set the configured login bonus point value.
- **Identical Travel Partners** - Copy the leader into all five character slots and lock the selection.

### Settings and Utilities

- **Manual Settings and Presets** - Save runtime settings and manage named presets; unsaved changes remain temporary.
- **Configurable Hotkeys** - Bind a single keyboard key to runtime actions or presets; new bindings default to `None`.
- **Show Info** - Display game/data versions, server address, Keychip ID, and player IDs with independent field recovery.
- **Server Address Masking** - Hide the server address in the information overlay.
- **Show FPS** - Display the current frame rate.
- **Dummy Login** - Provide simulated card/QR login input; player-ID login remains unresolved on the current CN version.
- **Custom Camera IDs** - Select the camera devices used for Chime, QR scanning, and photos.
- **Block Coin** - Bypass credit availability checks and selected payment operations.
- **Custom Version Text** - Replace the game's displayed version label.
- **Disable Mask** - Remove the configured display mask.
- **Disable Background** - Hide gameplay background elements.
- **Graphics Controls** - Adjust resolution and frame-rate settings from the panel.
- **Touch-to-Button Input** - Use mapped touch regions as menu button inputs outside gameplay.
- **Force Quick Retry** - Enable the game's quick-retry option outside its usual restrictions.
- **Network Logger** - Log game network requests and responses, with optional console output.
- **Unity Logger** - Capture Unity logs and optionally print them to the console.
- **Print User Information** - Print current player data for inspection.
- **Export User Data** - Write player data to local JSON files.
- **Automatic Data Backup** - Back up player data locally during the supported login flow.
- **Infinite Timer** - Prevent supported game timers from expiring, with a legacy implementation available.
- **Quick Boot** - Shorten startup waits; currently disabled in the initialization path.
- **Single Player Mode** - Simplify single-player operation and optionally hide the unused monitor.
- **Skip Warning Screen** - Skip the startup warning screen.
- **Skip Fade** - Shorten supported scene transitions.
- **Change Fade Style** - Switch the available transition animation style.
- **Ignore Information** - Suppress subsequent game information notices.
- **Change Default Options** - Load custom default play options for guest users.
- **Change Game Settings** - Override QR reading, photo capture/upload, and character selection settings.
- **Safe Mode** - Start without applying gameplay and compatibility patches.

### Compatibility Fixes

- **Disable Environment Check** - Bypass supported runtime environment checks on versions after 1.50.
- **Disable Encryption** - Disable the game's configured network encryption layer.
- **Disable INI Clear** - Prevent the game from clearing INI configuration files.
- **Disable Reboot** - Disable scheduled automatic reboots.
- **Fix Check Auth** - Preserve a valid HTTP/HTTPS game server address from authentication.
- **Fix Debug Input** - Restore supported debug keyboard input handling.
- **Force as Server** - Force the local machine into the server role.
- **Restore Certificate Validation** - Restore default TLS certificate validation for game HTTP connections.
- **Rewrite Judge Timing** - Override configured audio and note judgement timing offsets.
- **Skip Version Check** - Bypass the account version check during login.
- **Skip Cake Hash Check** - Skip the `Cake.dll` hash check.
- **Skip Special Number Check** - Override the game's special-number check result.

## Usage Notes

- Runtime settings and presets use `RuntimeSettings.yml`; global shortcuts use `KeyBindConfig.yml`. Startup module switches remain in `Config.yml`.
- In **Hotkeys**, click `None` to capture a key. Click again or press Escape to cancel; click an existing binding to clear it. Save manually to persist bindings.
- Practice sessions do not save or upload song scores. Exit through **Exit practice and restart whole song** to resume normal scoring.
- Score generation and practice support ordinary solo and local two-player charts; special modes are excluded. Unreachable rank/combo combinations are reported.
- Batch defaults to 10 songs at the current difficulty, without crossing categories. Low-level boost is off by default; its initial threshold is displayed level 12+.
- Do not run Batch alongside AquaMai ImmediateSave or another automatic score uploader. Unknown upload outcomes pause the batch and require review before continuation.
- Audio synchronization, actual server saves, and repeated batch track resets still require in-game verification.

## Development

1. Install the [.NET Framework 4.7.2 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472).
2. Put compatible SDGB reference assemblies in `Libs` and build `Sinmai-Assist.csproj`.
3. Copy `Output/Sinmai-Assist.dll` into the game's `Mods` folder.

Offline tests require the .NET 10 SDK:

```powershell
dotnet run --project Tests/ScorePlanner.Tests.csproj
dotnet run --project Tests/Flows/Flows.csproj
```

Optional native metadata and chart checks require `ilspycmd` and local game files:

```powershell
dotnet run --project Tests/ScorePlanner.Tests.csproj -- "GAME/Sinmai_Data/Managed/Assembly-CSharp.dll" "GAME/Sinmai_Data/StreamingAssets" "Output/Sinmai-Assist.dll"
```

These tests do not launch the game or send upload requests. Game files, dependencies, logs, and build output are excluded from the repository.

## Links

- [MelonLoader Wiki](https://melonwiki.xyz/#/modders/quickstart)
- [Harmony Documentation](https://harmony.pardeike.net/articles/patching-prefix.html)
