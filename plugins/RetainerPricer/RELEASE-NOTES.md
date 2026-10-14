## 0.5.0.21

- Restored up to 100 items per Sniper history batch in every scope and history mode.
- The automatic failed-item rescan now starts after a 15-second cooldown. Automatic and manual failed-item rescans wait 5 seconds after each history request finishes, including the retry of a temporarily failed request.
- Initial scans keep their normal one-second minimum between request starts and one retry for temporary errors. Rescans request only unresolved items; successful history and live watching are preserved. Smaller-batch fallbacks remain disabled.
