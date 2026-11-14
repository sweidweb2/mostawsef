# Shared UI Components Catalog

The full list of shared components for the Mostawsef desktop app (Avalonia).

**Status**
- **Built:** a custom component exists in `frontend/Mostawsef.Desktop/Components/`.
- **Built-in:** an Avalonia control, styled by the theme. Use it directly, no wrapper needed.
- **Deferred:** not built yet. Build it when a screen first needs it (see the rules in `.cursor/rules/`).
  Rows marked *Removed while unused* were built early for the gallery and deleted; restore them from git history (commit `983d1b9`) when a screen needs one.

All components use the design tokens in `Styles/Tokens/` (no hard-coded colors), support Light/Dark, and mirror correctly in Arabic (RTL).
Styling, localization and responsiveness are explained in [ui-foundation.md](ui-foundation.md).

Usage namespace: `xmlns:ui="using:Mostawsef.Desktop.Components"`.

---

## Layout

| Component | Status | Use |
| --- | --- | --- |
| AppShell (sidebar + top bar + content area) | Built | `ui:AppShell` |
| Sidebar / NavMenu | Built | `ui:NavMenu` + `ui:NavMenuItem` inside `AppShell.Sidebar`; a standalone `NavMenuItem` runs its `Command` (e.g. sign out) |
| TopBar | Built | `ui:TopBar` |
| BrandMark (logo tile + app title) | Built | `ui:BrandMark` (sidebar headers), `Classes="lg"` stacked and large (login) |
| PreferencesBar (theme + language switches) | Built | `ui:PreferencesBar` (login screen, portal and gallery top bars) |
| PageHeader (title, breadcrumb, actions) | Built | `ui:PageHeader` |
| Card | Built | `ui:Card` (classes: `outlined`, `flat`, `interactive`) |
| Section / Panel | Built | `ui:Section` |
| SplitView (list + details) | Deferred | Removed while unused (`ListDetailsView`) |
| TabView | Built-in | `TabControl` / `TabItem` |
| Divider | Built | `ui:Divider` (optional `Text`, `Orientation`) |
| ResponsiveGrid (layout helper) | Built | `ui:ResponsiveGrid` (auto 1-4 columns) |
| PageHost (shell content area) | Built | `ui:PageHost Page="{Binding CurrentPage}"`: shows a page view model's view; caches views and toggles `IsVisible` (keeps DataGrids mounted) |
| CachedContentHost | Built | Like ContentControl but caches each content's view and toggles `IsVisible` (New Order steps) |

## Forms

| Component | Status | Use |
| --- | --- | --- |
| TextField | Built-in | `TextBox` |
| TextArea | Built-in | `TextBox Classes="textarea"` |
| NumberField | Built-in | `NumericUpDown` |
| PasswordField | Built | `ui:PasswordField` |
| SearchBox | Built | `ui:SearchBox` (`SearchCommand` on Enter, Esc clears) |
| DatePicker | Built-in | `CalendarDatePicker` / `DatePicker` |
| TimePicker | Built-in | `TimePicker` |
| DateRangePicker | Deferred | Removed while unused; two `CalendarDatePicker`s for now |
| Dropdown / Select | Built-in | `ComboBox` |
| AutoComplete (patient and drug lookup) | Built-in | `AutoCompleteBox` |
| Checkbox | Built-in | `CheckBox` |
| RadioGroup | Built-in | `RadioButton` with shared `GroupName` |
| ToggleSwitch | Built-in | `ToggleSwitch` |
| FileUpload / ImagePicker | Deferred | Removed while unused |
| FormField (label + input + validation message) | Built | `ui:FormField` (`Label`, `Hint`, `Error`, `IsRequired`); in a `FormViewModel` form: `Error="{l:FieldError FullName}"` |
| FormSection | Deferred | Use `ui:ResponsiveGrid MaxColumns="2"` of `ui:FormField`s inside a `ui:Section` |
| ValidationSummary | Deferred | Removed while unused; per-field errors via `ui:FormField Error` |

## Buttons

| Component | Status | Use |
| --- | --- | --- |
| Button (primary / secondary / danger / ghost) | Built-in | `Button Classes="primary"` (also `secondary`, `danger`, `ghost`, `link`, sizes `sm`, `lg`); `ghost` + `Classes.selected="{Binding ...}"` marks the chosen option of a small switch (lab tests table / grid) |
| IconButton | Built | `ui:IconButton Icon="{StaticResource IconEdit}"` |
| CopyButton | Built | `ui:CopyButton Classes="sm" Text="{Binding Phone}"`: copies Text to the clipboard, shows a check mark for a moment, hidden while Text is empty. Put it beside table values people copy (names, phones, codes) |
| SplitButton / DropdownButton | Built-in | `SplitButton`, `DropDownButton` |
| ButtonGroup | Deferred | Removed while unused |

## Data display

| Component | Status | Use |
| --- | --- | --- |
| DataTable (sorting, paging, selection) | Built | `ui:DataTable` wrapping a `DataGrid` (loading, empty and error states with `HasError` + `RetryCommand`, toolbar, footer). Every table starts with a `#` column of `ui:RowNumber` cells and binds `FirstRowNumber="{Binding FirstRowNumber}"` (both list view model bases provide it), so numbering continues across pages. List pages keep one `ListPageItems<T>` for the grid (`ReplaceAll` on reload; `PrependNewest` after create when possible). Prefer text columns + `ValueConverters` for money/dates; template columns for badges, actions and row numbers. Rows are `TableRowHeight` tall and, on hover, get a light tint and an accent bar on the start edge (styled once in `Styles/Controls/DataGrid.axaml`) |
| Pagination | Built | `ui:Pagination` (`CurrentPage`, `PageCount`) |
| ListView | Built-in | `ListBox` |
| DetailsView (label/value pairs) | Built | `ui:DetailsView` + `ui:DetailsItem Label Value` (`IsCode` for JSON / stack traces) |
| Badge / StatusTag | Built | `ui:Badge Text="Paid" Kind="Success"` (`Neutral`, `Primary`, `Success`, `Warning`, `Danger`, `Info`) |
| Avatar | Built | `ui:Avatar` (initials or image); `MaxInitials="1"` for the first letter only, `IsColorful="True"` for one of five tones picked from the name (same name, same color), e.g. beside patient names |
| StatCard (dashboard numbers) | Built | `ui:StatCard Title Value Caption Icon Kind` |
| Chart | Deferred | |
| Timeline (patient history) | Deferred | |
| Calendar / Scheduler (appointments) | Deferred | Removed while unused (day / week `Scheduler`) |
| Tooltip | Built-in | `ToolTip.Tip="..."` |

## Feedback

| Component | Status | Use |
| --- | --- | --- |
| Toast / Notification | Built | `ToastService.Show(...)` |
| Alert / Banner | Built | `ui:Alert Kind="Danger" Text="..."` (`Info`, `Success`, `Warning`, `Danger`; hidden while `Text` is empty). Form-level errors: `Text="{l:TrKey {Binding FormErrorKey}}"` |
| LoadingSpinner | Built | `ui:Spinner` |
| LoadingOverlay | Built | `ui:LoadingOverlay IsLoading="..."` |
| SkeletonLoader | Deferred | |
| ProgressBar | Built-in | `ProgressBar` (classes: `success`, `warning`, `danger`) |
| EmptyState | Built | `ui:EmptyState` |
| ErrorState | Built | `ui:ErrorState RetryCommand="..."` (`Title`, `Description`, `Icon`; Retry button shows only with a command) |

The portal shell shows a connection `ui:Alert` (`ConnectionBanner`) above every page while the server is unreachable
or runs another API version; pages don't add their own.

## Overlays

| Component | Status | Use |
| --- | --- | --- |
| Modal / Dialog | Built | `ui:Modal` shown with `DialogService.ShowAsync(...)` (hosted by `ui:DialogHost` in MainWindow). The X button, Esc and a click outside (press and release both outside) dismiss it through `ConfirmDismiss`; `ShowCloseButton="False"` turns all three off |
| ConfirmDialog | Built | `DialogService.ConfirmAsync(...)` |
| Drawer / SidePanel | Deferred | |
| ContextMenu | Built-in | `ContextMenu` / `MenuFlyout` |
| Popover | Built-in | `Flyout FlyoutPresenterClasses="popover"` |

## Navigation

| Component | Status | Use |
| --- | --- | --- |
| Breadcrumb | Deferred | Removed while unused; `ui:PageHeader.Breadcrumb` accepts any content |
| Stepper / Wizard | Deferred | Removed while unused |
| Tabs | Built-in | `TabControl` |

## Clinic-specific (shared across pages)

Display-only. They take plain values (text, status) and contain no business logic.

| Component | Status | Use |
| --- | --- | --- |
| PatientCard / PatientHeader | Deferred | Removed while unused |
| AppointmentCard | Deferred | |
| PrescriptionItem | Deferred | |
| InvoiceSummary | Deferred | |
| VitalsDisplay | Deferred | |
| PrintPreview (prescriptions, invoices, reports) | Deferred | Removed while unused |

---

## Adding a new component

1. Check this list and Avalonia's built-in controls first. Prefer styling a built-in control over building a new one.
2. Create the class in `Components/<Category>/<Name>.cs` (namespace `Mostawsef.Desktop.Components`).
3. Add its `ControlTheme` to `Components/<Category>/<Category>Themes.axaml`. Use tokens only:
   `{DynamicResource}` for `Brush*` / theme shadows, `{StaticResource}` for fixed tokens.
4. Make sure the category's themes file is included in `Styles/Theme.axaml`.
5. Use `{l:Tr Key}` for any built-in text and add the key to both `Resources/Strings.resx` and `Strings.ar.resx`.
6. Add it to the component gallery and update its row in this file.
