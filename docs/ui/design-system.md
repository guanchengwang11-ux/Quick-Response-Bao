# Quick Response Bao UI foundation

Phase 1 establishes the visual and technical contract for the UI refactor. It intentionally does not replace the current shell or page content.

## Resource order

`App.xaml` loads resources in this order:

1. WPF UI `ThemesDictionary`;
2. WPF UI `ControlsDictionary`;
3. the active Quick Response Bao semantic theme;
4. Quick Response Bao tokens, typography, icons, and controls;
5. the active localization dictionary.

Quick Response Bao resources therefore remain the application-facing contract. Pages should use `DynamicResource` keys prefixed with `Qrb`; direct hexadecimal colors are reserved for the theme palette files.

## Files

- `Resources/DesignTokens.xaml`: spacing, radius, control height, focus, motion, and compatibility tokens.
- `Resources/Typography.xaml`: the six semantic text styles.
- `Resources/Controls.xaml`: button, input, card, badge, empty state, info bar, and focus contracts.
- `Resources/Icons.xaml`: Fluent System Icon sizes (16, 20, and 24 px).
- `Resources/LightTheme.xaml`: semantic light palette.
- `Resources/DarkTheme.xaml`: semantic dark palette.
- `Resources/HighContrastTheme.xaml`: Windows system-color fallback.

## Usage rules

Use semantic resources rather than visual values:

```xaml
<Border Background="{DynamicResource QrbSurfaceBrush}"
        BorderBrush="{DynamicResource QrbBorderBrush}"
        Style="{DynamicResource QrbCardStyle}">
  <TextBlock Text="Page title" Style="{DynamicResource QrbPageTitleTextStyle}" />
</Border>
```

Buttons use `QrbPrimaryButtonStyle`, `QrbSecondaryButtonStyle`, `QrbGhostButtonStyle`, `QrbDangerButtonStyle`, or `QrbIconButtonStyle`. Asynchronous actions may use `AsyncButton` and `QrbAsyncButtonStyle`; its loading state disables repeated activation.

## Theme behavior

The persisted `Light`, `Dark`, and `System` values are unchanged. `ThemeService` maps them to WPF UI `ApplicationThemeManager`, then overlays the matching Quick Response Bao semantic palette. System changes continue to be observed through Windows user-preference notifications. High Contrast maps essential semantic resources to Windows system colors.

## Phase boundary

The page classes in `Views/Pages` are compile-verified skeletons for the future navigation host. They are not connected to `MainWindow` in Phase 1. Candidate window HWND flags, no-activate behavior, Hook handling, replacement, SendInput, clipboard, positioning, and scrolling code are unchanged.

## Visual verification

Baseline screenshots are in `docs/ui/before`. The Phase 1 control gallery is in `docs/ui/phase-1`, including Light, Dark, focus, bilingual typography, controls, feedback, and 125/150/175 percent render captures. Windows 10 remains supported because the app targets `net8.0-windows`/win-x64 and WPF UI ships its Fluent System Icon font with the package.
