# Quick Response Bao 1.0.0-rc.3

This prerelease candidate fixes the RC.2 duplicate-trigger-text blocker and is intended for manual validation only. It is not the formal v1.0.0 release.

- Candidate confirmation now carries the normalized query, actual typed-character count, captured target HWND/PID/process, confirmation method, and capture time.
- The default insertion flow validates and restores the original target, removes the actual raw search characters, and only then pastes the complete response.
- Raw input such as `how  to` searches as `how to` but deletes all seven characters before insertion.
- A failed deletion prevents paste; a failed paste triggers a best-effort Unicode restoration of the original typed search text.
- Mouse clicks outside the non-activating candidate window and target/application changes invalidate stale confirmation context.
- A persisted bilingual setting can disable replacement and retain insert-at-caret behavior.
- Diagnostics now show captured/confirmation windows, focus restoration, deletion count/result, paste result, and replacement method without sensitive text.

Download both RC files with `checksums.txt` from the GitHub Actions artifact and verify SHA-256 before testing.
