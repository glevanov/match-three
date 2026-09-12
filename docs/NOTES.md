# Match Three — Notes

Live issues and recurring dev/tooling gotchas. Keep this file short: if a
note is no longer surprising or actionable, delete it. Put stable structure
in [DESIGN.md](DESIGN.md) and settled product choices in
[DECISIONS.md](DECISIONS.md).

## Known issues

- **Reshuffle quality at 9×9/6 is still a placeholder.** The current
  Fisher-Yates rejection approach misses often enough that Zen can hit
  "No moves left" earlier than ideal. Consider a smarter reshuffle or
  placement strategy during tuning.
- **Headless self-tests can print shutdown leak warnings.** If a run prints
  `SELFTEST-OK-*` and exits 0, ignore trailing
  `ObjectDB instances were leaked at exit` / `resources still in use`
  messages caused by one-shot SFX still playing during teardown.

## Android self-tests

Use `scripts/selftest-android.sh --selftest-flow` (or another
`--selftest-*` flag) to run headless tests on a connected phone.

- First produce a base debug export with `scripts/export-android.sh`.
- The script patches the current managed assemblies into that APK, runs the
  app, reads PASS/FAIL from logcat, and exits accordingly.
- It also hides the Android-specific patching details such as sparse-PCK
  metadata updates and forwarding self-test flags via `assets/_cl_`.

## 16 KB page alignment

Android/device builds require `net9.0` and 16 KB-aligned native libs. This
repo targets `net9.0` everywhere; use `scripts/export-android.sh` for device
builds and do not reintroduce `android:pageSizeCompat`.

If you need to verify an APK manually:

```sh
unzip -q -d /tmp/apk build/matchthree-debug.apk 'lib/arm64-v8a/*'
for f in /tmp/apk/lib/arm64-v8a/*.so; do
    readelf -lW "$f" | awk '/^  LOAD/{print "'"$f"'", $NF}'
done   # every p_align must be 0x4000
zipalign -c -P 16 -v 4 build/matchthree-debug.apk
```

## Android export

Use `scripts/export-android.sh` for phone builds and deploys.

- Safe path: `godot --headless --export-debug "Android"`
- Do **not** combine export with `--build-solutions`; that can produce an
  APK missing managed assemblies.
- The script can validate, install, and launch the APK for you.

Typical use:

```sh
scripts/export-android.sh --install --run
```

Ignore unrelated device-log noise such as
`FeatureFlagsImplExport ... package android.xr` or `gralloc5`.

## Ad-hoc Godot runs

A bare `godot --headless ...` needs `dotnet` on PATH. If not, Godot's .NET
host can fail or crash before startup. Use:

```sh
PATH="$HOME/.dotnet:$PATH" godot --headless --import
```

`DOTNET_ROOT` is not required.

## Debug screens

The Debug menu exposes two quick manual checks:

- `Scenes/DebugStarGlow.tscn` / `View/DebugStarGlow.cs` for gem-effect
  visuals on device.
- `Scenes/DebugSounds.tscn` / `View/DebugSounds.cs` for one-shot SFX previews.
  Preview buttons are in `no_ui_click`, so they do not also play the global
  UI click.

If you need to boot directly into a debug scene on device, temporarily change
`run/main_scene` in `project.godot`, export/run, then revert.
