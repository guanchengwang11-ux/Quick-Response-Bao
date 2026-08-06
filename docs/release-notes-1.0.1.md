# Quick Response Bao 1.0.1

Quick Response Bao 1.0.1 is a maintenance release focused on candidate-list usability, response validation, keyword consistency, and safe bulk imports.

## Improvements

- Candidate lists now support non-activating mouse-wheel scrolling, visible scrollbars, selection visibility, and predictable reset-to-top behavior when results refresh.
- Response content is consistently limited to 300 characters in the editor, repository, copy, and import paths. Invalid rows are reported without truncating data or stopping the remaining import.
- Keyword matching remains optional and now consistently supports exact, partial, and case-insensitive matching with normalized delimiters and Excel/CSV/JSON aliases.
- Excel, CSV, and JSON imports share validation and duplicate detection. Existing records and duplicates within the same file are skipped without changing stored usage or update metadata.
- Import results distinguish validation failures, duplicates, and other skipped rows, including source-row references where available.

## Release files

- `Quick-Response-Bao-Setup-1.0.1-x64.exe`: x64 installer that preserves user data during upgrades.
- `Quick-Response-Bao-Portable-1.0.1-x64.zip`: self-contained x64 portable package.
- `checksums.txt`: SHA-256 hashes for both release packages.

## Upgrade notes

Updating from v1.0.0 preserves the database, settings, backups, and logs under `%LocalAppData%\QuickResponseBao`. The updater still requires a matching entry in `checksums.txt` and will not install a package whose checksum fails verification.

## Known limitations

- Compatibility with third-party application updates can vary; use the Diagnostics page when caret positioning or paste behavior changes.
- Automatic paste intentionally does not send messages.
