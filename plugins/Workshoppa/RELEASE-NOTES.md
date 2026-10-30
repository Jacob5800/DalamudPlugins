## 9.3.13.4

- Improve disable/update cleanup: stop callbacks first, attempt remaining cleanup after an error, and ignore repeated shutdown calls.
- Keep attempting transfer cancellation and external-plugin setting restoration if another cleanup step fails.
