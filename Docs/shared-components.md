# Shared UI Components Catalog

The full list of shared components for the Mostawsef desktop app (Avalonia).

**Status**
- **Built:** a custom component exists in `frontend/Mostawsef.Desktop/Components/`.
- **Built-in:** an Avalonia control, styled by the theme. Use it directly, no wrapper needed.
- **Deferred:** not built yet. Build it when a screen first needs it (see the rules in `.cursor/rules/`).

All components use the design tokens in `Styles/Tokens/` (no hard-coded colors), support Light/Dark, and mirror correctly in Arabic (RTL).
Styling, localization and responsiveness are explained in [ui-foundation.md](ui-foundation.md).

Usage namespace: `xmlns:ui="using:Mostawsef.Desktop.Components"`.

---

## Layout

| Component | Status | Use |
| --- | --- | --- |
| AppShell (sidebar + top bar + content area) | Built | `ui:AppShell` |
| Sidebar / NavMenu | Built | `ui:NavMenu` + `ui:NavMenuItem` inside `AppShell.Sidebar` |
| TopBar | Built | `ui:TopBar` |
| PageHeader (title, breadcrumb, actions) | Built | `ui:PageHeader` |
| Card | Built | `ui:Card` (classes: `outlined`, `flat`, `interactive`) |
| Section / Panel | Built | `ui:Section` |
| SplitView (list + details) | Built | `ui:ListDetailsView` (stacks below 900 px) |
| TabView | Built-in | `TabControl` / `TabItem` |
| Divider | Built | `ui:Divider` (optional `Text`, `Orientation`) |
| ResponsiveGrid (layout helper) | Built | `ui:ResponsiveGrid` (auto 1-4 columns) |

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
| DateRangePicker | Built | `ui:DateRangePicker` (`From`, `To`) |
| Dropdown / Select | Built-in | `ComboBox` |
| AutoComplete (patient and drug lookup) | Built-in | `AutoCompleteBox` |
| Checkbox | Built-in | `CheckBox` |
| RadioGroup | Built-in | `RadioButton` with shared `GroupName` |
| ToggleSwitch | Built-in | `ToggleSwitch` |
| FileUpload / ImagePicker | Built | `ui:FileUpload`, `ui:ImagePicker` |
| FormField (label + input + validation message) | Built | `ui:FormField` (`Label`, `Hint`, `Error`, `IsRequired`) |
| FormSection | Built | `ui:FormSection` (responsive columns, `MaxColumns`) |
| ValidationSummary | Built | `ui:ValidationSummary` (`Errors`) |

## Buttons

| Component | Status | Use |
| --- | --- | --- |
| Button (primary / secondary / danger / ghost) | Built-in | `Button Classes="primary"` (also `secondary`, `danger`, `ghost`, `link`, sizes `sm`, `lg`) |
| IconButton | Built | `ui:IconButton Icon="{StaticResource IconEdit}"` |
| SplitButton / DropdownButton | Built-in | `SplitButton`, `DropDownButton` |
| ButtonGroup | Built | `ui:ButtonGroup` |

## Data display

| Component | Status | Use |
| --- | --- | --- |
| DataTable (sorting, paging, selection) | Built | `ui:DataTable` wrapping a `DataGrid` (loading + empty states, toolbar, footer) |
| Pagination | Built | `ui:Pagination` (`CurrentPage`, `PageCount`) |
| ListView | Built-in | `ListBox` |
| DetailsView (label/value pairs) | Deferred | |
| Badge / StatusTag | Built | `ui:Badge Text="Paid" Kind="Success"` (`Neutral`, `Primary`, `Success`, `Warning`, `Danger`, `Info`) |
| Avatar | Built | `ui:Avatar` (initials or image) |
| StatCard (dashboard numbers) | Deferred | |
| Chart | Deferred | |
| Timeline (patient history) | Deferred | |
| Calendar / Scheduler (appointments) | Built | `ui:Scheduler Items="{Binding Appointments}"` (day / week view, `SchedulerItem` entries) |
| Tooltip | Built-in | `ToolTip.Tip="..."` |

## Feedback

| Component | Status | Use |
| --- | --- | --- |
| Toast / Notification | Built | `ToastService.Show(...)` |
| Alert / Banner | Deferred | |
| LoadingSpinner | Built | `ui:Spinner` |
| LoadingOverlay | Built | `ui:LoadingOverlay IsLoading="..."` |
| SkeletonLoader | Deferred | |
| ProgressBar | Built-in | `ProgressBar` (classes: `success`, `warning`, `danger`) |
| EmptyState | Built | `ui:EmptyState` |
| ErrorState | Deferred | Use `ui:EmptyState` with an error icon for now |

## Overlays

| Component | Status | Use |
| --- | --- | --- |
| Modal / Dialog | Built | `ui:Modal` shown with `DialogService.ShowAsync(...)` (hosted by `ui:DialogHost` in MainWindow) |
| ConfirmDialog | Built | `DialogService.ConfirmAsync(...)` |
| Drawer / SidePanel | Deferred | |
| ContextMenu | Built-in | `ContextMenu` / `MenuFlyout` |
| Popover | Built-in | `Flyout FlyoutPresenterClasses="popover"` |

## Navigation

| Component | Status | Use |
| --- | --- | --- |
| Breadcrumb | Built | `ui:Breadcrumb` + `ui:BreadcrumbItem` |
| Stepper / Wizard | Built | `ui:Stepper`, `ui:Wizard` + `ui:WizardPage` |
| Tabs | Built-in | `TabControl` |

## Clinic-specific (shared across pages)

Display-only. They take plain values (text, status) and contain no business logic.

| Component | Status | Use |
| --- | --- | --- |
| PatientCard / PatientHeader | Built | `ui:PatientCard`, `ui:PatientHeader` |
| AppointmentCard | Deferred | |
| PrescriptionItem | Deferred | |
| InvoiceSummary | Deferred | |
| VitalsDisplay | Deferred | |
| PrintPreview (prescriptions, invoices, reports) | Built | `ui:PrintPreview` (visual shell; real printing comes later) |

---

## Adding a new component

1. Check this list and Avalonia's built-in controls first. Prefer styling a built-in control over building a new one.
2. Create the class in `Components/<Category>/<Name>.cs` (namespace `Mostawsef.Desktop.Components`).
3. Add its `ControlTheme` to `Components/<Category>/<Category>Themes.axaml`. Use only `DynamicResource` tokens.
4. Make sure the category's themes file is included in `Styles/Theme.axaml`.
5. Use `{l:Tr Key}` for any built-in text and add the key to both `Resources/Strings.resx` and `Strings.ar.resx`.
6. Add it to the component gallery and update its row in this file.
