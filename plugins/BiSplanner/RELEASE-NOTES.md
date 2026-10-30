## 0.3.0.4

- Improve disable/update cleanup: stop callbacks first, attempt remaining cleanup after an error, and ignore repeated shutdown calls.
- Fix the inventory event handler remaining attached after disabling the plugin, guard pending refreshes, and stop queued actions during shutdown.
