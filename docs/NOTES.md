# Notes / Known issues

- **Reshuffle inefficiency at 9×9/6:** Fisher-Yates rejection sampling misses ≈80% of
  the time within 20 retries. Zen mode will hit "No moves left" earlier than ideal.
  Consider min-conflict or biased placement during tuning. For now it's a
  graceful-end placeholder.
- **Timer value 75s is placeholder.** Tune against actual play.
- **Cosmetic shutdown warnings in headless self-tests.** Runs that quit while a
  one-shot SFX is still playing print `ObjectDB instances were leaked at exit`
  and `N resources still in use at exit` (with `--verbose`, naming e.g.
  `res://Assets/Audio/swipe.mp3`). The `SELFTEST-OK-*` line is still printed
  and the process exits 0 — nothing to chase. Visible on `--selftest-swap`
  too, so it is not tied to any one sound asset.

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

### 16 KB page alignment (resolved in the .NET 9 Android target)

On Android 16 devices that can use 16 KB pages, an APK whose native libs are
not 16 KB ELF-aligned (`p_align=0x4000`) triggers an "app doesn't support
16 KB pages" compatibility dialog at launch, and that dialog can swallow
touches until dismissed. Godot 4.7.2's own libs (`libgodot_android.so`,
`libc++_shared.so`) are aligned; the Mono runtime packs shipped with earlier
.NET versions (`libmonosgen-2.0.so`, `libmono-component-*.so`,
`libSystem.*.so`) had `p_align=0x1000` and 4 KB-congruent LOAD segments,
which cannot be fixed by flipping ELF headers — they need a re-link. .NET 9
ships re-linked packs, and Godot 4.5+ requires Android exports to target
`net9.0` (godotengine/godot#110263).

The game, `Engine/` and `Tests/` therefore all target `net9.0`
unconditionally (Godot's generated template would keep an older TFM for
non-Android and switch only under `GodotTargetPlatform=android`, but one TFM
everywhere keeps the engine and tests on the same runtime). On desktop the
Godot .NET host rolls forward to the latest installed major runtime
(`rollForward: LatestMajor`), so the net9.0 assembly loads there too.
**Every build now needs the .NET 9 SDK/runtime** (`dotnet --list-sdks`).

Verify an export with:

```sh
unzip -q -d /tmp/apk build/matchthree-debug.apk 'lib/arm64-v8a/*'
for f in /tmp/apk/lib/arm64-v8a/*.so; do
    readelf -lW "$f" | awk '/^  LOAD/{print "'"$f"'", $NF}'
done   # every p_align must be 0x4000
zipalign -c -P 16 -v 4 build/matchthree-debug.apk
```

The earlier `android:pageSizeCompat="enabled"` manifest workaround was
removed once the libs were aligned: forcing page-size compat mode would keep
the app off native 16 KB pages. `scripts/patch-debug-apk.py --no-debuggable`
still exists for producing non-debuggable APKs by hand, but is no longer
needed for this warning.

## Dev tooling: Android export/deploy script

Use `scripts/export-android.sh` for phone builds. It:

- ensures Godot can find `dotnet` (prepends `$HOME/.dotnet` when needed; builds need a 9.0 SDK there — see the 16 KB section)
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

## Dev tooling: ad-hoc Godot runs

The repo scripts prepend `$HOME/.dotnet` themselves, but a bare `godot
--headless ...` — for example the `godot --headless --import` an asset swap
needs — requires `dotnet` on PATH too. Without it Godot's .NET host fails
(`One of the dependent libraries is missing` / `Failed to load hostfxr`) and
the process dies with SIGSEGV; that crash is the missing PATH, not a project
problem. Run ad-hoc Godot commands as:

```sh
PATH="$HOME/.dotnet:$PATH" godot --headless --import
```

`DOTNET_ROOT` is not required (Godot derives it from `dotnet` on PATH).

## Dev tooling: debug screens

The main menu now exposes a single **Debug** entry which opens `Scenes/DebugMenu.tscn` (+ `View/DebugMenuScreen.cs`), a small debug submenu with two screens:

- `Scenes/DebugStarGlow.tscn` (+ `View/DebugStarGlow.cs`) — the standalone
  3×3 gem-effect screen: static gems at ~2.5× game size on a dark backdrop;
  top row Star, middle Flame, bottom plain (red / orange / blue per column;
  Star ids 12 & 512 are anti-phase so a bright and a dim pulse always
  coexist). Keep it; it is the fast loop for eyeballing gem effects on a
  device.
- `Scenes/DebugSounds.tscn` (+ `View/DebugSounds.cs`) — a button-per-sound SFX
  preview screen for the board's one-shot audio (swipe / pop / flame / star /
  hypercube / birth). No music on this screen.

If you want to boot directly into the gem-effect screen on a phone (then
REVERT the main scene afterwards):

```sh
sed -i 's#run/main_scene="res://Scenes/Menu.tscn"#run/main_scene="res://Scenes/DebugStarGlow.tscn"#' project.godot
scripts/export-android.sh --install --run
```
