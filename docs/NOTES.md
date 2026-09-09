# Notes / Known issues

- **Reshuffle inefficiency at 9×9/6:** Fisher-Yates rejection sampling misses ≈80% of
  the time within 20 retries. Zen mode will hit "No moves left" earlier than ideal.
  Consider min-conflict or biased placement during tuning. For now it's a
  graceful-end placeholder.
- **Timer value 75s is placeholder.** Tune against actual play.

## Dev tooling: Android export/deploy script

Use `scripts/export-android.sh` for phone builds. It:

- ensures Godot can find `dotnet` (prepends `$HOME/.dotnet` when needed)
- exports with the safe .NET flow: `godot --headless --export-debug "Android"`
- intentionally avoids `--build-solutions` during export (that previously produced a broken APK with missing managed assemblies)
- validates that the APK actually contains `MatchThree.dll`, `MatchThree.Engine.dll`, and `GodotSharp.dll`
- optionally installs and launches via `adb`
- suppresses one known spurious Godot 4.7.2 headless-export warning about `export/android/shutdown_adb_on_exit`

Typical use:

```sh
scripts/export-android.sh --install --run
```

If you still see `FeatureFlagsImplExport ... package android.xr` or `gralloc5`
lines in device `logcat`, those are platform/driver noise, not this repo's
export flow.

## Dev tooling: effect debug screen

`Scenes/DebugStarGlow.tscn` (+ `View/DebugStarGlow.cs`) is a standalone 3×3
screen — static gems at ~2.5× game size (dark backdrop): top row Star gems,
middle row Flame gems, bottom row plain ones (red / orange / blue per column;
Star ids 12 & 512 are anti-phase so a bright and a dim pulse always coexist).
Keep it; it is the fast loop for eyeballing gem effects on a device.

To launch it on the phone (then REVERT the main scene afterwards):

```sh
sed -i 's#run/main_scene="res://Scenes/Menu.tscn"#run/main_scene="res://Scenes/DebugStarGlow.tscn"#' project.godot
scripts/export-android.sh --install --run
```
