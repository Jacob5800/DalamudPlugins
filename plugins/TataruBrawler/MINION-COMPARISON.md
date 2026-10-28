# Minion KeyboundBrawler comparison

Inspected the user-supplied KeyboundBrawler.zip (Typer.lua, helper/KeyboundOcr.cs, Loop.lua, Lib.lua and Shop.lua). Archive comments are historical observations, not proof of current runtime behavior. No OCR/helper process or shop automation was imported.

The useful background design is window-directed key-down/up delivery rather than foreground SendInput or direct input-field writes. The Dalamud implementation is independent, uses only its own FFXIV process window, retains per-key duty/input/cast gates and does not need the external OCR helper. The user already had the background option enabled. English key progression is checked offline; actual window delivery needs game observation.

Potential additions, not implemented:

- Configurable threat/label priority: Minion explicitly orders green, then gold, then nearby enemies. Our current priorities emphasize barrier/casting urgency and on-screen approach. Native label mapping and tradeoffs should be verified before changing it.
- Optional punctuation normalization: the Minion reference treats ? ! - and apostrophes as optional, preserving spaces, periods and commas. Verify current game acceptance before shortening our answers.
- MGC cap warnings or an optional stop threshold. Minion also has prioritized shop purchases and repeatable coffers; purchasing would require separate user choices and authorization.

OCR itself is not an improvement here: the Dalamud build already reads native prompt data and avoids OCR ambiguity and helper-process management.
