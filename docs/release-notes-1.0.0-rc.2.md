# Quick Response Bao 1.0.0-rc.2

This prerelease candidate fixes the RC.1 compatibility blockers and is intended for manual validation only. It is not the formal v1.0.0 release.

- Corrected the native `SendInput` paste shortcut structure, result validation, permission diagnostics, and clipboard restoration timing.
- Improved Lark and Chromium-style input detection: unknown UI Automation state no longer blocks a whitelisted application; explicit password evidence remains blocked.
- Added foreground-process capture, running-process selection, focused child-process diagnostics, and case-insensitive whitelist handling.
- Added continuous phrase search with letters and normalized spaces, including trailing-space refresh and reset rules.
- Redesigned the main navigation, editor validation, diagnostic presentation, operation feedback, and bilingual resources.
- Added a new customer-support visual identity and application/installer icon.

Download both RC files with `checksums.txt` from the GitHub Actions artifact and verify SHA-256 before testing.
