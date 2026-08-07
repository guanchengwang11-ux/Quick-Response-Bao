# Quick Response Bao 1.0.2

Quick Response Bao 1.0.2 replaces the previous 300-character response-body limit with language-aware limits that support practical long responses while preventing unbounded content.

## Improvements

- English and other whitespace-separated response text now supports up to 600 words. Consecutive spaces, tabs, and line breaks are treated as a single separator, and punctuation attached to a word does not create extra words.
- Chinese, Japanese, Korean, and related CJK content supports up to 3000 Unicode characters, so text without spaces cannot bypass length validation.
- Mixed content is validated independently against both limits: up to 600 words and up to 3000 CJK characters.
- The same rule now applies before database writes and across add, edit, copy, Excel, CSV, and JSON import paths.
- The response editor displays live word/CJK counters, warning near either limit and showing a clear localized error after a limit is exceeded.
- Import failures now identify whether the word limit, CJK limit, or both limits caused the validation failure while preserving source-row details and processing remaining rows.

## Release files

- `Quick-Response-Bao-Setup-1.0.2-x64.exe`: x64 installer that preserves user data during upgrades.
- `Quick-Response-Bao-Portable-1.0.2-x64.zip`: self-contained x64 portable package.
- `checksums.txt`: SHA-256 hashes for both release packages.

## Upgrade notes

Updating preserves the database, settings, backups, and logs under `%LocalAppData%\QuickResponseBao`. Existing response content is not truncated or removed. The updater still requires a matching entry in `checksums.txt` and will not install a package whose checksum fails verification.

## Known limitations

- Compatibility with third-party application updates can vary; use the Diagnostics page when caret positioning or paste behavior changes.
- Automatic paste intentionally does not send messages.
