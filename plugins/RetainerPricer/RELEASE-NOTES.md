## 0.5.0.18

- Removed the 50-item Sniper history fallback. Failed requests are no longer split into smaller batches, reducing time spent on failing requests during the initial scan.
- Temporary request failures still get one retry at the original batch size. Unresolved items are automatically retried once after the full scan and its five-second cooldown; manual retry remains available.
- History requests still run one batch at a time with at least one second between request starts. Live alerts continue during the scan and retry pass.
