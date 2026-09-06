# Notes / Known issues

- **Reshuffle inefficiency at 9×9/6:** Fisher-Yates rejection sampling misses ≈80% of
  the time within 20 retries. Zen mode will hit "No moves left" earlier than ideal.
  Consider min-conflict or biased placement during M4/M5 tuning. For now it's a
  graceful-end placeholder.
- **Timer value 75s is placeholder.** Tune post-M5 against actual play.
