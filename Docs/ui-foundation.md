# Desktop UI Foundation

How the Mostawsef desktop app (Avalonia 12) is styled, localized and laid out.
Component list and status: [shared-components.md](shared-components.md).

---

## 1. Files

| File | Contents |
| --- | --- |
| `Styles/Theme.axaml` | **Theme file**: the single entry point that assembles everything (included once in `App.axaml` after `<FluentTheme />`) |
| `Styles/Tokens/Colors.axaml` | Brand palette, neutral scale, semantic colors (Light + Dark) |
| `Styles/Tokens/Typography.axaml` | Font, size scale, line heights, weights |
| `Styles/Tokens/Spacing.axaml` | 4 px spacing scale, gaps, paddings |
| `Styles/Tokens/Radius.axaml` | Corner radius scale |
| `Styles/Tokens/ShadowTokens.axaml` + `Shadows.axaml` | Elevation levels (Light + Dark) + `shadow-*` classes |
| `Styles/Tokens/Sizes.axaml` | Control heights, icons, avatars, borders, focus ring, shell and window sizes |
| `Styles/Tokens/Breakpoints.axaml` | Responsive breakpoints |
| `Styles/Tokens/ZIndex.axaml` | Layer order |
| `Styles/Tokens/Effects.axaml` | Opacity and motion durations |
| `Styles/Tokens/Icons.axaml` | Icon geometries |
| `Styles/Responsive.axaml` | **Responsiveness engine**: container-query utility classes |
| `Styles/Controls/*.axaml` | Styles for built-in controls (text, buttons, inputs, tabs, menus, DataGrid) |

Golden rule: components and views use **tokens only** (`{DynamicResource ...}`), never raw values.

---

## 2. Colors

### Brand palette and the 60-30-10 rule

| Share | Role | Colors | Used for |
| --- | --- | --- | --- |
| 60% | Dominant / neutral | `#FFFFFF`, `#EAF3FB` | Surfaces (cards, sidebar, dialogs) and the app background |
| 30% | Secondary | `#B7D6F0`, `#4A90C8` | Selected states, chips, brand borders; icons, focus ring, info, hover accents |
| 10% | Accent / primary | `#1F5A8A` | Primary buttons, active navigation, links, key numbers |

**Contrast:** white text on `#1F5A8A` is about 7.2:1 (passes AA). White text on `#4A90C8` is only about 3.4:1, so `#4A90C8` is never used behind normal-size text.

Palette primitives (reference for new tokens and charts): `Brand50..Brand900` (brand blue scale; 100, 200, 400 and 600 are the given colors) and `Neutral0..Neutral950` (blue-tinted grays).

### Semantic colors (Light / Dark)

| Group | Keys | Light |
| --- | --- | --- |
| Primary | `BrushPrimary`, `...Hover`, `...Pressed`, `BrushOnPrimary`, `BrushPrimarySubtle`, `BrushPrimarySubtleText` | `#1F5A8A`, `#184A72`, `#12395A`, white, `#EAF3FB`, `#1F5A8A` |
| Secondary | `BrushSecondary`, `...Hover`, `BrushSecondarySubtle`, `BrushOnSecondarySubtle` | `#4A90C8`, `#3478B0`, `#B7D6F0`, `#12395A` |
| Background / surface | `BrushBackground`, `BrushSurface`, `BrushSurfaceAlt`, `BrushSurfaceRaised`, `BrushSurfaceSelected`, `BrushOverlay` | `#EAF3FB`, white, `#F4F8FC`, white, `#EAF3FB`, navy 40% |
| Text | `BrushText`, `BrushTextMuted`, `BrushTextDisabled`, `BrushTextInverse`, `BrushTextLink` | `#132434`, `#5B6B7C`, `#9AA8B6`, white, `#1F5A8A` |
| Border | `BrushBorder`, `BrushBorderStrong`, `BrushBorderBrand`, `BrushDivider`, `BrushFocus` | `#DDE4EB`, `#C5D0DB`, `#B7D6F0`, `#EDF1F5`, `#4A90C8` |
| Success | `BrushSuccess`, `...Subtle`, `...Text`, `...Border` | `#1E8E5A`, `#E6F5EE`, `#146141`, `#A7DCC3` |
| Warning | `BrushWarning`, `...Subtle`, `...Text`, `...Border` | `#C77700`, `#FFF4E0`, `#8A5300`, `#F5D29A` |
| Error | `BrushDanger`, `...Hover`, `...Subtle`, `...Text`, `...Border`, `BrushOnDanger` | `#C93C3C`, `#A83030`, `#FDECEC`, `#8F2424`, `#F3B9B9`, white |
| Info | `BrushInfo`, `...Subtle`, `...Text`, `...Border` | `#4A90C8`, `#EAF3FB`, `#1F5A8A`, `#B7D6F0` |
| Disabled | `BrushDisabled`, `BrushDisabledText`, `BrushDisabledBorder` | `#EDF1F5`, `#9AA8B6`, `#DDE4EB` |

Dark mode is derived from the same palette: navy surfaces (`#0C1722` / `#122130`), and a lighter primary `#6AA6D6` with dark text on it (about 6:1).

Switching: `ThemeService.SetTheme(AppTheme.Dark)` / `Toggle()`; saved in `%AppData%\Mostawsef\settings.json`.

---

## 3. Typography

Font: **IBM Plex Sans Arabic** (Arabic + Latin). No letter spacing (it breaks Arabic joining).

| Class | Size | Line height | Weight |
| --- | --- | --- | --- |
| `display` | 36 | 48 | Bold |
| `h1` | 30 | 42 | Bold |
| `h2` | 24 | 34 | SemiBold |
| `h3` | 20 | 30 | SemiBold |
| `h4` | 18 | 28 | SemiBold |
| `body-lg` | 16 | 26 | Regular |
| `body` (default) | 14 | 22 | Regular |
| `caption` | 12 | 18 | Regular |

Color/weight classes: `muted`, `primary`, `link`, `success`, `warning`, `danger`, `strong`.

## 4. Spacing (4 px grid)

| Token | px | Typical use |
| --- | --- | --- |
| `Space0` / `SpaceHalf` | 0 / 2 | hairline offsets |
| `Space1` / `Space2` | 4 / 8 | inside controls, icon-to-text |
| `Space3` / `Space4` | 12 / 16 | between fields, card inner gaps |
| `Space5` / `Space6` | 20 / 24 | card padding, page padding |
| `Space8` / `Space10` / `Space12` / `Space16` | 32 / 40 / 48 / 64 | between page sections, empty states |

Gaps: `GapTight` 4, `GapControls` 8, `GapStack` 12, `GapGrid` 16, `GapSection` 32.
Paddings: `PaddingControl` 12,6 (`Sm` 8,4, `Lg` 16,10), `PaddingInput` 10,6, `PaddingChip` 10,2, `PaddingCard` 20 (`Compact` 12), `PaddingDialog` 24, `PaddingPage` 24 (`Compact` 16).

## 5. Radius

`RadiusNone` 0, `RadiusXs` 4 (tooltips, tags), `RadiusSm` 6, `RadiusMd` 8 (buttons, inputs), `RadiusLg` 12 (cards, tables, dialogs), `RadiusXl` 16, `RadiusFull` (pills, badges, avatars).

## 6. Shadows (elevation)

Navy-tinted in Light, stronger black in Dark. `ShadowNone`, `ShadowXs` (inputs, rows), `ShadowSm` (cards), `ShadowMd` (hover, dropdowns), `ShadowLg` (dialogs), `ShadowXl` (toasts, floating panels). Classes: `shadow-xs` ... `shadow-xl` on any Border.

## 7. Sizes

| Group | Tokens |
| --- | --- |
| Controls | `ControlHeightSm` 28, `ControlHeight` 36, `ControlHeightLg` 44, `TouchTargetMin` 44 |
| Icons | `IconSizeSm` 14, `IconSize` 18, `IconSizeLg` 24, `IconSizeXl` 40 |
| Avatars | `AvatarSizeSm` 32, `AvatarSize` 40, `AvatarSizeLg` 64 |
| Borders | `BorderWidth` 1, `BorderWidthThick` 2, `FocusRingWidth` 2 |
| Shell | `SidebarWidth` 240, `SidebarCompactWidth` 64, `TopBarHeight` 56, `DrawerWidth` 420, `DialogMaxWidth` 560 (`Lg` 800) |
| Content | `ContentMaxWidth` 1440, `FormMaxWidth` 960, `WindowMinWidth` 720, `WindowMinHeight` 520 |

## 8. Z-index

`ZIndexBase` 0, `Raised` 1, `Sticky` 10, `Dropdown` 100, `Overlay` 1000, `Modal` 1100, `Toast` 1200, `Tooltip` 1300 (C#: `ZIndexes`).
In Avalonia, ZIndex only orders siblings in one panel; popups, flyouts and tooltips already render in their own layers.

## 9. Effects

Opacity: `OpacityDisabled` 0.5, `OpacityMuted` 0.7, `OpacityVeil` 0.75 (loading veil).
Motion: `DurationFast` 120 ms (hover, press), `DurationNormal` 200 ms (sidebar, expand), `DurationSlow` 300 ms (dialogs, drawers).

---

## 10. Responsiveness engine

Layouts react to the **width of the AppShell content area**, not the window or device.

| Breakpoint | Width | Behavior |
| --- | --- | --- |
| Compact | < 900 | 1 column, page padding 16, header actions wrap, list/details stack |
| Medium | 900 - 1279 | 2 columns |
| Wide | 1280 - 1599 | 3 columns |
| XWide | >= 1600 | 4 columns, content capped at 1440 |

- `.page` on a page root: correct padding per breakpoint + max width.
- Visibility: `hide-compact` / `show-compact` (900), `hide-medium` / `show-medium` (1280), `hide-wide` / `show-wide` (1600).
- `stack-compact`: horizontal StackPanel becomes vertical below 900.
- `ResponsiveGrid`: auto columns from `MinColumnWidth` and `MaxColumns`; `ui:ResponsiveGrid.ColumnSpan="0"` spans a row.
- `AppShell` collapses the sidebar below `AutoCompactWidth` (1100 px); `ListDetailsView` stacks below 900.
- C# constants: `Breakpoints.Medium / Wide / XWide`.

## 11. Localization and RTL

- Languages: **Arabic** (default, RTL) and **English**, switchable at runtime with `ui:PreferencesBar` (or `LocalizationService.Toggle()`).
- `LocalizationService` is a DI singleton: inject it into view models/services; code not created by DI (controls) uses `App.Localization`.
- Strings in `Resources/Strings.resx` + `Resources/Strings.ar.resx`. XAML: `{l:Tr Key}`, with a bound argument
  `{l:Tr Dashboard.Welcome, Arg=UserName}`, or a key held by the view model `{l:TrKey ErrorKey}`. Code: `localization.Get(key)`.
- View models expose resource keys, not translated text, and never subscribe to `LanguageChanged` (only attached controls may).
- Culture: `ar-EG` (Gregorian, Latin digits) / `en-US`. Layout, margins, borders and icons mirror automatically.
- New windows must call `App.Localization.Apply(window)`.

## 12. App services (frontend)

| Service | Purpose |
| --- | --- |
| `SettingsService` | Loads and saves `settings.json` (theme, language) |
| `ThemeService` | Light / Dark switching (`ui:PreferencesBar` in the UI) |
| `LocalizationService` | Language, culture, RTL, translations |
| `DialogService` | `ShowAsync(modal)`, `ConfirmAsync(message, ...)` on the window's `DialogHost` |
| `ToastService` | `Show(message, title, kind)`, `Success(...)`, `Error(...)` |

## 13. Gallery and tools

- `Views/Dev/GalleryView.axaml`: every built component with theme and language toggles (`dotnet run --project frontend/Mostawsef.Desktop`).
- `scripts/capture-window.ps1`: saves a PNG of the running app window.
