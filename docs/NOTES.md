# Notes / Known issues

- **Reshuffle inefficiency at 9×9/6:** Fisher-Yates rejection sampling misses ≈80% of
  the time within 20 retries. Zen mode will hit "No moves left" earlier than ideal.
  Consider min-conflict or biased placement during tuning. For now it's a
  graceful-end placeholder.
- **Timer value 75s is placeholder.** Tune against actual play.

## Dev tooling: effect debug screen

`Scenes/DebugStarGlow.tscn` (+ `View/DebugStarGlow.cs`) is a standalone 3×3
screen — static gems at ~2.5× game size (dark backdrop): top row Star gems,
middle row Flame gems, bottom row plain ones (red / orange / blue per column;
Star ids 12 & 512 are anti-phase so a bright and a dim pulse always coexist).
Keep it; it is the fast loop for eyeballing gem effects on a device.

To launch it on the phone (then REVERT the main scene afterwards):

```sh
sed -i 's#run/main_scene="res://Scenes/Menu.tscn"#run/main_scene="res://Scenes/DebugStarGlow.tscn"#' project.godot
/tmp/godot_mono/Godot_v4.7.2-stable_mono_linux_x86_64/godot --headless --export-debug "Android" build/matchthree-debug.apk
adb install -r build/matchthree-debug.apk
adb shell am force-stop com.matchthree && adb shell am start -n com.matchthree/com.godot.game.GodotAppLauncher
```
