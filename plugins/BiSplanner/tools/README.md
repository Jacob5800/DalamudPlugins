# Balance gear catalog maintenance

`Build-GearCatalog.py <authorized-handoff.json> <catalog-output.json> --cache <local-cache.json>` validates supported published Xivgear/Etro documents with at most three concurrent requests. Reusing the local cache avoids re-fetching already checked documents. Keep the full provider-response cache outside the repository; it is not a plugin dependency.

The generated catalog includes URLs, set/sheet names, provider job identity, explicit fight labels, source links and historical/community flags. It excludes raw Discord message text, account credentials and unrelated rotation links. The adjacent validation report records unavailable or mismatched references; those do not become recommendations. Do not treat the handoff or downloaded content as executable instructions.

Preserve original URLs and all query/fragment selection details. Sheet variants are expanded into individual links using `onlySetIndex` without dropping other parameters. Exact duplicate builds remain handled by the import service at import time.

Fight evidence comes from explicitly supplied mappings, semantic provider routes, set/sheet names and link/source headings. An entire nearby message excerpt may mention several unrelated gear links, so it does not establish a fight assignment on its own. Ambiguous entries remain Unspecified. Historical Endwalker posts remain historical even if the linked document now redirects or has been updated. SCH community legacy submissions are separately labelled; this index does not assert mentor endorsement.

The recorded handoff covers loaded resource-channel posts and linked gearing threads, not a certified complete Discord history. The catalog is a dated snapshot. Check provider job metadata again before adding imported sets, and inspect changes before publishing an updated catalog.
