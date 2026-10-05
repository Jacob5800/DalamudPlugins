# Suggesting portrait looks

Open an issue with a descriptive name, the intended effect, and the style values you personally previewed in the game's Portrait Editor. The maintainer curates additions to `catalog/styles.json`; pull requests are not accepted for catalog or source changes while the repository uses its all-rights-reserved notice.

Do not include another player's character image, character data, or preset code without their permission. The catalog stores only lighting, camera-distance and small camera-target framing adjustments, not portrait images or character-specific presets.

Each entry has this shape:

```json
{
  "seed": 9,
  "id": "example-look",
  "name": "Example Look",
  "category": "Warm",
  "description": "A short explanation of the look.",
  "directionalColor": { "red": 250, "green": 190, "blue": 120 },
  "ambientColor": { "red": 100, "green": 75, "blue": 60 },
  "directionalBrightness": 1.0,
  "ambientBrightness": 1.0,
  "cameraDistance": 1.0,
  "cameraTargetOffset": 0.0
}
```

Limits enforced by the plugin:

- `id`: 1–64 ASCII letters, digits, `_` or `-`; must be unique.
- `name`: at most 80 characters.
- `category`: at most 40 characters.
- `description`: at most 240 characters.
- Brightness multipliers: `0.6`–`1.4`.
- Camera distance multiplier: `0.75`–`1.3`.
- Camera target offset: `-0.08`–`0.08` of the camera-to-target distance; keep it subtle.
- RGB channel values: `0`–`255`.

The catalog is not sorted or labeled by popularity unless a public, auditable voting method is added. The plugin does not send votes, usage data, or character details to the catalog host.
