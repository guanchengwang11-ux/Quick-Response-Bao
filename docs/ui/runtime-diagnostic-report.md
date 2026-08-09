# Candidate runtime, scrolling, and UI performance report

Date: 2026-08-09
Branch: `codex/ui-ux-refactor`

## Executive result

The duplicate candidate presentation and modifier-key buffer defects were fixed first. The application-wide scrolling defect was then traced to an unbounded `NavigationView` content measurement: pages received effectively infinite height, ordinary pages never formed a scroll viewport, and the response `DataGrid` realized every row. The shell now supplies a finite page viewport; ordinary pages use one reusable `ScrollablePageLayout`, while data pages retain native virtualized control scrolling.

The Release executable was exercised with runtime tracing enabled. With 336 real responses, the Library realized 7–9 rows instead of 336, preserved its position at offset 120 after navigation, and reused every cached page instance. Settings changed from offset 0 to 120. No page-specific `PreviewMouseWheel` patches remain.

## 1. Candidate runtime diagnosis

- The apparent second candidate was the Windows input-method `TextInputHost`, not a second Quick Response Bao candidate HWND. Runtime enumeration showed one Quick Response Bao candidate window.
- The actual modifier defect was that only generic `VK_SHIFT`, `VK_CONTROL`, and `VK_MENU` were treated as modifiers. Left/right variants reset the query buffer. All generic/left/right Shift, Ctrl, Alt and Caps Lock variants are now ignored as modifiers.
- `SuggestionPresentationController` owns a generation, sequence, normalized query and captured target. Stale Dispatcher work is discarded before and after search.
- `risk`, Shift+`USER`, and CapsLock+`USER` diagnostic runs each produced one current presentation and one Quick Response Bao candidate HWND.
- Runtime trace is opt-in through `QRB_RUNTIME_TRACE=1`; synthetic input acceptance additionally requires `QRB_ACCEPT_SYNTHETIC_INPUT=1`. Production still rejects injected hook events and logs no complete user input.

## 2. Unified scrolling architecture

| Area | Architecture | Runtime result |
|---|---|---|
| Shell | Finite viewport calculated from `NavigationView`, title bar and header | Page no longer receives unbounded height |
| Dashboard / Import / Diagnostics / Settings / About | `ScrollablePageLayout` with one native `ScrollViewer`, vertical auto, horizontal disabled, vertical panning | Settings extent 1838.6, viewport 680, offset 0 → 120; other tested pages fit their viewport |
| Library | Native `DataGrid` scroll viewer, row/column virtualization, recycling | extent 336 rows, viewport 6, offset 0 → 120, 7–9 rows realized |
| Categories | Native virtualized `DataGrid` scroll viewer | Current category data fits; overflowing DataGrid is covered by STA integration test |
| Applications | Native recycling `ListBox` scroll viewer | Current five-item whitelist fits; overflowing ordinary/control viewport is covered by STA integration test |

The original failure affected unrelated pages because the common shell content presenter measured its child without a finite vertical viewport. Nested or missing page scroll viewers were symptoms. The fix is at the shell/page-layout boundary, not a collection of mouse-wheel handlers. Native routed input and `PanningMode` are retained, so precision-touchpad input is not intercepted by custom wheel code.

## 3. Window chrome

The shell uses WPF UI `FluentWindow` + `TitleBar` with `ExtendsContentIntoTitleBar=True`; it does not draw three unrelated replacement buttons. Automated Release-EXE UI Automation produced:

| Action | Result |
|---|---|
| Minimize | Pass (`SW_SHOWMINIMIZED`) |
| Maximize | Pass (`SW_SHOWMAXIMIZED`) |
| Restore | Pass (`SW_SHOWNORMAL`) |
| Close | Pass; window hidden and process/listener remained alive |
| Single-instance tray reopen | Pass; secondary exited and primary became visible |

The primary-display maximized bounds used the current monitor work area (outer non-client border extends by the normal DWM frame). Second-monitor drag/maximize and physical touchpad behavior remain manual hardware tests.

## 4. Navigation and responsiveness measurements

Measurements are from the Release executable, 336 real responses, 150% Windows scaling, with `QRB_UI_TRACE=1`. “Interactive” includes page refresh, layout and Dispatcher `ContextIdle`.

| Page | Cold interactive | Warm interactive | Cache reused |
|---|---:|---:|---|
| Dashboard | 75.48 ms walkthrough navigation | 14.35 ms | Yes |
| Library | 184.41 ms | 116.46 ms | Yes; filter, selection and offset retained |
| Categories | 120.43 ms | 125.07 ms | Yes |
| Import / Export | 47.10 ms | 26.29 ms | Yes |
| Applications | 43.21 ms | 23.26 ms | Yes |
| Diagnostics | 70.47 ms | 31.91 ms | Yes |
| Settings | 112.60 ms | 40.39 ms | Yes |
| About | 21.06 ms | 17.05 ms | Yes |

The header/content assignment occurs before asynchronous refresh. Library and Categories still spend about 16–25 ms above the 100 ms warm target on a complete re-layout after reattachment, but visual pressed/header feedback is not delayed by database I/O.

UI-stall sample for the startup plus full two-pass walkthrough: P50 19.76 ms, P95 95.76 ms, maximum 648.09 ms; 14 samples over 16 ms, 8 over 33 ms, 7 over 50 ms, 1 over 100 ms and 1 over 250 ms. The maximum is initial shell construction, not a warm page action.

Startup stages on the same machine: settings 55–63 ms, SQLite ready 117–139 ms, main window visible 739.99 ms, cache/listener ready 931.98 ms, main window interactive 1016.32 ms. The main window is now shown before candidate-window, hook and tray initialization; one repository snapshot is shared by management UI and runtime search instead of reading the database twice. Update checking remains outside the critical startup path.

## 5. Synchronous work and cache audit

- Diagnostics UI Automation inspection moved to a worker task; warm Diagnostics navigation fell from about 337 ms to 18–32 ms.
- Process enumeration on the Applications page moved to a worker task.
- Categories no longer reads all responses on every visit and reuses the in-memory snapshot.
- Settings displays the in-memory settings snapshot and persists only after a change.
- Clipboard retry waits are asynchronous; the candidate transaction no longer sleeps the UI Dispatcher during clipboard contention.
- All eight pages use lazy page caching. Search/filter/selection/Library offset survive navigation when data did not change.
- Add, edit, copy, toggle and delete update management/runtime caches incrementally. Batch and category-wide mutations perform one authoritative reload, not the former `ReloadCacheAsync + MainViewModel.RefreshAsync + Page.RefreshAsync` chain.
- Runtime candidate search cache and management facet index are distinct objects.

## 6. DataGrid and filter implementation decision

| Project | License / state | Strength | Integration risk | Decision |
|---|---|---|---|---|
| [dotnet/DataGridExtensions](https://github.com/dotnet/DataGridExtensions) | MIT; established .NET Foundation project | Attaches to native DataGrid; text/boolean filtering and custom templates | Excel-style faceted counts and draft semantics still require substantial custom work; styles can conflict with WPF UI headers | Reference only |
| [macgile/FilterDataGrid-Beta](https://github.com/macgile/FilterDataGrid-Beta) | MIT; very small history, .NET 5-era sample | Excel-like value popup and template-column support | Low maintenance signal, replacement DataGrid/style surface, older target | Rejected |
| [janproch/fastwpfgrid](https://github.com/janproch/fastwpfgrid) | MIT; no releases | Very fast bitmap/data-virtualized rendering | Does not use normal WPF binding/templates; incompatible with current template columns, accessibility and WPF UI styling | Rejected |

The chosen implementation keeps native WPF `DataGrid` virtualization and adds `ResponseLibraryFacetIndex` plus an asynchronous UI adapter. Facet counts apply all other columns while ignoring the opened column. The popup has a loading state, sort ascending/descending, value search, tri-state select all, draft selection, counts, result preview, Apply, Clear and Cancel/Escape. Closing outside cancels the draft. Summary/keyword high-cardinality fields use text operators rather than thousands of checkboxes. Keyword cells now render one text visual (`risk · compliance · +2`) instead of a chip control tree per row.

## 7. 10,000-row benchmark

Thirty warmed iterations per size, Release test process:

| Rows | Candidate/search P50 | Candidate/search P95 | Facet P50 | Facet P95 |
|---:|---:|---:|---:|---:|
| 10 | 0.001 ms | 0.002 ms | 0.002 ms | 0.002 ms |
| 100 | 0.026 ms | 0.029 ms | 0.013 ms | 0.015 ms |
| 1,000 | 0.101 ms | 0.860 ms | 0.079 ms | 0.082 ms |
| 10,000 | 0.553 ms | 4.947 ms | 0.798 ms | 1.259 ms |

The 10,000-row STA WPF integration test verifies a finite viewport, fewer than 100 realized rows, native vertical range, and changed vertical offset. These algorithm numbers exclude native target-window focus recovery and candidate-window rendering; the five target applications still require final human latency observation.

## 8. Automated and manual coverage

- 264 automated tests pass.
- Runtime tests instantiate real WPF controls on an STA thread and verify page scrolling, DataGrid virtualization and offset changes.
- `tools/test-window-chrome.ps1` launches the real Release executable and verifies title-bar state transitions plus tray reactivation.
- Release executable autowalk covers Dashboard → Library → Categories → Import/Export → Applications → Diagnostics → Settings → About twice, captures navigation timings, scroll ranges, row realization and cache identity.
- Candidate injection, focus, replacement and clipboard tests remain unchanged and passing.

Still manual: precision-touchpad momentum, mouse wheel over every specified child hit target, second-monitor work-area behavior, 100/125/175/200% spot checks on this exact build, 60 fps slow-motion feedback review, and Lark/Telegram/Discord/Chrome/Edge candidate latency/focus regression.

## 9. Screenshots

- `docs/ui/runtime/about-titlebar.png`: complete shell and native title-bar controls at 150% scaling.
- `docs/ui/runtime/settings-scroll.png`: Settings page at a non-zero scroll offset with the native scrollbar visible.

The Library screenshot was intentionally not captured from the real user database because it could expose response text. Existing sanitized UI screenshots remain under `docs/ui/final/`.
