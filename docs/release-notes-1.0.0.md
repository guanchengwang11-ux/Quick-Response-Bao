# Quick Response Bao 1.0.0

Quick Response Bao 1.0.0 is the first stable release of the Windows quick-response assistant.

## Highlights

- Global keyboard monitoring with an independent, non-activating candidate window for Lark, Telegram, Discord, Chrome, and Edge.
- Fast bilingual response search with summaries, keyword highlighting, keyboard navigation, and mouse selection.
- Safe replacement of the typed search text before inserting a response, including raw character tracking for repeated and trailing spaces.
- Clipboard restoration and no automatic message sending.
- Complete response and category management, bulk operations, Excel import/export, and database backup/restore.
- Light, dark, and follow-system themes with Simplified Chinese and English switching.
- Stable update checks through GitHub Releases with exact asset matching and mandatory SHA-256 verification.
- Privacy-safe diagnostics, three-level candidate positioning fallback, and compatibility troubleshooting tools.

## Distribution

- `Quick-Response-Bao-Setup-1.0.0-x64.exe`: x64 installer with desktop/start-menu shortcuts and user-data-preserving upgrades.
- `Quick-Response-Bao-Portable-1.0.0-x64.zip`: self-contained x64 portable package; no Visual Studio or separately installed .NET runtime is required.
- `checksums.txt`: SHA-256 checksums for both release files.

User data is stored under `%LocalAppData%\QuickResponseBao` and is kept separate from installed program files.
