### 0.5.0.10

- History days 0 (any age) now uses one history batch at a time, with at least one second between request starts, including retries and smaller fallback requests.
- Fixed-window scans keep two concurrent batches. Both modes retain 100-item starting batches, 50/25-item fallback and the 500-sale cap. This change reduces request pressure; persistent Universalis timeouts may still occur.
