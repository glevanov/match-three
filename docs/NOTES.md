# Notes / Known issues

- **Reshuffle inefficiency at 9×9/6:** Fisher-Yates rejection sampling misses ≈80% of
  the time within 20 retries. Zen mode will hit "No moves left" earlier than ideal.
  Consider min-conflict or biased placement during tuning. For now it's a
  graceful-end placeholder.
- **Timer value 75s is placeholder.** Tune against actual play.

## Dev tooling: on-device headless self-tests

`scripts/selftest-android.sh --selftest-flow` (also `--selftest-swap`,
`--selftest-reject`, `--selftest-burst=N`, `--selftest-timer`,
`--selftest-special`, `--selftest-hypercube`, `--selftest-menu`) builds the
current sources, patches them into the existing debug export and runs the test
on the connected phone — no Godot binary needed. It prints PASS/FAIL from
logcat and exits accordingly. `scripts/patch-debug-apk.py` is the underlying
patcher (also usable to patch a build by hand).

Two Android export quirks make that patch path non-obvious:

- Godot 4.7 records every file's size + md5 in the APK's sparse PCK
  (`assets/assets.sparsepck`) and slices the asset to the recorded size, so a
  replaced `MatchThree.dll` only loads when that entry is updated (otherwise
  the runtime reports `.NET: Failed to open assembly image`). The patcher does
  this.
- Launch-intent extras are ignored (`com.godot.game.GodotApp` is not exported),
  so `--selftest-*` flags are packed into `assets/_cl_` instead. On desktop the
  flags stay user args after `--`.

The patcher also re-aligns the APK for 16 KB page devices (`zipalign -P 16`),
required on Android 16 / targetSdk 36.

### The "app doesn't support 16 KB pages" warning

On Android 16 devices that can use 16 KB pages, a debuggable APK whose native
libs are not 16 KB ELF-aligned triggers a compatibility dialog at launch, and
that dialog can swallow touches until dismissed. Godot 4.7.2's own libs
(`libgodot_android.so`, `libc++_shared.so`) are already 16 KB aligned; the
offenders are the **.NET 8 Mono runtime packs** (`libmonosgen-2.0.so`,
`libmono-component-*.so`, `libSystem.*.so`): their LOAD segments are
`p_align=0x1000` and only 4 KB congruent, so they cannot be fixed by
flipping ELF headers — they need a re-link. .NET added 16 KB alignment in
.NET 9 (Godot 4.5+ targets `net9.0` for Android), so moving the project off
the pinned Godot 4.7.2 / `net8.0` stack is the real fix.

Until then the same dialog is suppressed by either of these:

- `scripts/export-android.sh` ensures
  `android/build/src/main/AndroidManifest.xml` carries
  `android:pageSizeCompat="enabled"` (API 36 attribute; the template's
  `compileSdk` is 36) before every export. The app then explicitly runs in
  page-size compat mode and Android does not show the warning, while the
  build stays debuggable. The edit lives in the script because the `android/`
  build template is gitignored (generated locally by the editor).
- For an APK that is already built, `scripts/patch-debug-apk.py ...
  --no-debuggable` flips `android:debuggable` to false in the binary
  manifest, which suppresses the same warning (verified on device: dialog
  with the debug build, no dialog after the patch), at the cost of
  `adb shell run-as` and editor remote debugging.

Tapping "Don't show again" in the dialog also stops it for that device, and
the choice survives reinstalls until the app is uninstalled.

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
