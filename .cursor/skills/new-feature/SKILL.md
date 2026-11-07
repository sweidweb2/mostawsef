---
name: new-feature
description: >-
  Builds a complete new feature module end to end (entity, database migration, service, API endpoints,
  Contracts DTOs, desktop list page, create/edit form, translations and tests) by copying the users module.
  Use when the user asks to add a new feature, module, page, screen, CRUD, or a business area such as
  patients, appointments, doctors, drugs, services or invoices.
---

# New feature (copy of the users module)

The users module is the template. For every part below, open the users file named in the checklist, copy its shape,
and rename. Read the real file each time; do not write from memory. Project rules still apply
(`.cursor/rules/*.mdc`); this skill only lists the parts and the order.

## Step 1: Settle the feature with the user

Ask (AskQuestion) only what the request leaves open:

- **Fields**: name, type (text, number, yes/no, date, date and time, money, choice from a list), required, max length,
  unique.
- **Portal**: which role uses it (`Admin` or `Super`, or both).
- **List**: which columns, default sort, which fields the search box matches.
- **Actions** besides add, edit and delete (e.g. deactivate, print). Delete is soft by default (medical data);
  hard delete only if the user says so.
- **Readable number** (e.g. `P-000123`) needed?
- **Choice lists**: a fixed code enum or an editable table? No project decision yet (roadmap item 9): ask.

Do not add fields, rules or actions the user did not ask for.

## Step 2: Names

Pick these once and use them everywhere (example: patients):

| Thing | Pattern | Example |
| --- | --- | --- |
| Feature (plural) | `<Feature>` | `Patients` |
| Entity (singular) | `<Entity>` | `Patient` |
| Table | lower-case plural | `patients` |
| API route | `api/<kebab-plural>` | `api/patients` |
| Activity codes | `<feature>.<verb>` | `patients.created` |
| Resource keys | `<Feature>.*`, `Nav.<Feature>` | `Patients.Add`, `Nav.Patients` |
| Desktop folder | the portal role | `ViewModels/Admin/`, `Views/Admin/` |

## Step 3: Build it

Copy this checklist into the reply and tick it off:

```
Backend
- [ ] Domain entity
- [ ] EF configuration + DbSet
- [ ] Repository interface + implementation
- [ ] Contracts (rules, DTOs, requests)
- [ ] Service (+ activity codes, error codes)
- [ ] DI registrations
- [ ] Controller
- [ ] Migration
Desktop
- [ ] <Feature>Api + registration
- [ ] List view model + view
- [ ] Form view model + view
- [ ] AppRoutes
- [ ] English + Arabic strings
Tests and docs
- [ ] API integration tests
- [ ] Desktop tests
- [ ] Docs
- [ ] Build + tests pass
```

### Backend

1. **Entity**: `backend/Mostawsef.Domain/Entities/<Entity>.cs`, copy of `User.cs`.
   - Derive from `SoftDeletableEntity` (or `Entity` for a hard delete). Never declare Id, audit fields or Version.
   - `public const int ...MaxLength` per text field, private parameterless constructor for EF, a public constructor
     with the required fields, `private set` properties, and methods that change state (`Rename`, `Deactivate`).
   - Searchable name: a `SearchName` column filled with `SearchText.Normalize(...)` wherever the name is set.
   - Money: `long` in minor units (piasters). Calendar dates such as a birth date: `DateOnly`. Points in time: `DateTime` (UTC).
2. **Configuration**: `backend/Mostawsef.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs`, copy of
   `UserConfiguration.cs`: `ToTable("<table>")`, `HasMaxLength`, `IsRequired`, unique indexes, enums
   `.HasConversion<string>()`. Tables that will hold many rows (patients, appointments) get the trigram index on
   `SearchName`, as in `LogConfigurations.cs` (`HasMethod("gin").HasOperators("gin_trgm_ops")`).
   Add `public DbSet<<Entity>> <Feature> => Set<<Entity>>();` to `Persistence/Context/AppDbContext.cs`.
3. **Repository**: `backend/Mostawsef.Application/Interfaces/I<Entity>Repository.cs` and
   `backend/Mostawsef.Infrastructure/Persistence/Repositories/<Entity>Repository.cs`, copies of `IUserRepository` /
   `UserRepository`: `GetByIdAsync`, `GetPageAsync(PageQuery)` (normalize the term with `SearchText.Normalize`,
   `Contains`, order, `SafePage`/`SafePageSize`), `Add`, `Remove`, `SaveChangesAsync`, plus an `...ExistsAsync` per
   unique field (`IgnoreQueryFilters()` only if deleted rows must keep the value reserved).
4. **Contracts**: `shared/Mostawsef.Contracts/<Feature>/<Entity>Contracts.cs`, copy of `UserContracts.cs`:
   - `<Entity>Rules` with the limits (same values as the entity constants).
   - `<Entity>ListItemDto` (the list columns) and `<Entity>DetailsDto` (all fields + `CreatedAt/By`, `UpdatedAt/By`, `Version`).
   - `Create<Entity>Request` / `Update<Entity>Request`: records with `init` properties and DataAnnotations whose
     `ErrorMessage` is an `ErrorCodes` constant. The update request carries `int Version`.
5. **Service**: `backend/Mostawsef.Application/Modules/<Feature>/<Entity>Service.cs`, copy of `UserService.cs`
   (interface and class in one file):
   - Not found: `AppException.NotFound()`. Unique value taken: `AppException.Conflict(code, field)`.
     Stale edit: compare `request.Version` with the entity and throw `AppException.Conflict(ErrorCodes.ConcurrencyConflict)`.
   - After each main action: `IActivityLogger.LogAsync(new ActivityEntry(...))` with `EntityType`, `EntityId` and
     non-sensitive `Details` (never medical notes or passwords).
   - Add `ActivityModules.<Feature>` and the `ActivityActions` codes in `Application/Common/ActivityActions.cs`.
     New business error codes go in `shared/Mostawsef.Contracts/Common/ErrorCodes.cs`.
   - Readable number: inject `INumberSequence`, `await numbers.NextAsync("<table>", ct)`, format with
     `DocumentNumber.Format("P", value)`, and store it in a unique column. Current time: inject `TimeProvider`.
6. **DI**: `services.AddScoped<I<Entity>Service, <Entity>Service>()` in `Application/DependencyInjection.cs`, and
   `services.AddScoped<I<Entity>Repository, <Entity>Repository>()` in `Infrastructure/DependencyInjection.cs`.
7. **Controller**: `backend/Mostawsef.Api/Controllers/<Feature>Controller.cs`, copy of `UsersController.cs`:
   `[Route("api/<kebab-plural>")]`, `[Authorize(Policy = AuthPolicies.AdminOnly or SuperOnly)]`, one-line actions,
   `CreatedAtAction` for create, `NoContent()` for delete, `ProducesResponseType` attributes.
8. **Migration** (from the repo root):
   ```powershell
   dotnet ef migrations add Add<Feature> --project backend/Mostawsef.Infrastructure --startup-project backend/Mostawsef.Api --output-dir Persistence/Migrations
   ```
   Read the generated file: only plain tables, columns and indexes (no raw SQL, `jsonb`, arrays or `citext`).

### Desktop

9. **API class**: `frontend/Mostawsef.Desktop/Services/Api/<Feature>Api.cs`, copy of `UsersApi.cs`; register it with
   `services.AddSingleton<<Feature>Api>()` in `ServiceRegistration.cs`.
10. **List page**: `ViewModels/<Role>/<Feature>ViewModel.cs`, copy of `UsersViewModel.cs`
    (`NumberedListViewModel<<Entity>ListItemDto>`, `FetchPageAsync`, Add/Edit/Delete commands, edit loads the details
    first with `ReadAsync`). View `Views/<Role>/<Feature>View.axaml` + `.axaml.cs`, copy of `UsersView`: `PageHeader`
    with Refresh/Add, `DataTable` with `SearchBox`, record count, columns, row action `IconButton`s and `Pagination`.
    Dates: `ValueConverters.LocalDateTime`; money: `MoneyFormat`; translated values: `{l:TrKey ..., Prefix=...}`.
11. **Form**: `ViewModels/<Role>/<Entity>FormViewModel.cs`, copy of `UserFormViewModel.cs` (`FormViewModel`;
    properties named like the request properties; `BuildRequest` trims text and passes `Version` on edit; `SubmitAsync`;
    `MarkClean()` last in the constructor). View `Views/<Role>/<Entity>FormView.axaml` + `.axaml.cs`, copy of
    `UserFormView`: `Alert` for `FormErrorKey` on top, one `ui:FormField` with `Error="{l:FieldError <Property>}"` per field.
    Calendar dates use `CalendarDatePicker` with `ValueConverters.DateOnly`; money inputs convert with
    `Money.ToMinor` / `Money.ToMajor` in the view model.
12. **Routes**: in `frontend/Mostawsef.Desktop/AppRoutes.cs` add `.View<<Entity>FormViewModel, <Entity>FormView>()` and
    `.Page<<Feature>ViewModel, <Feature>View>("Icon...", "Nav.<Feature>")` in the role's portal. Pick an icon from
    `Styles/Tokens/Icons.axaml`; add one there only if none fits.
13. **Strings**: add every key to both `Resources/Strings.resx` and `Resources/Strings.ar.resx`. Copy the `Users.*`
    set as a model: `Nav.<Feature>`, `<Feature>.Subtitle`, `.Add`, `.AddTitle`, `.EditTitle`, `.Records` ("{0} ..."),
    `.Col.*`, `.Field.*`, `.Saved`, `.Deleted`, `.DeleteConfirm`; plus `Action.<code>` for each activity code and
    `ErrorCode.<code>` for each new error code.

### Do not copy from users

Passwords and reset password, username normalization, roles, the own-account rule, `RecordLoginAsync`. They are
user-specific.

## Step 4: Tests

- **API**: `tests/Mostawsef.Api.IntegrationTests/<Feature>/<Feature>Tests.cs`, copy of `Users/UsersTests.cs`
  (`[Collection(ApiCollection.Name)]`, `factory.CreateClientAsAsync(TestUsers.X)`, `ProblemResponse.ReadAsync`).
  Cover: create then read back (trimmed values, `CreatedBy`, `Version` 0); 400 with one code per field; max length
  reports the limit; 409 for each unique field; 401 without a token; 403 on **every** endpoint for the other role;
  404 for an unknown id; update bumps `Version` and sets `UpdatedBy`; stale `Version` returns 409
  `concurrency_conflict`; delete hides the row (and soft delete keeps it in the database with `DeletedBy`); Arabic
  search finds names typed with or without hamza/ta marbuta/diacritics; paging; activity log codes recorded.
- **Desktop**: `tests/Mostawsef.Desktop.Tests/<Feature>/<Feature>PageTests.cs`, copy of `Users/UsersPageTests.cs`
  (`new TestServices()`, `app.SignInAs(...)`, `app.Api.OnGet/On(...)`, `ShowPage`, `CurrentForm`, `FieldError`).
  Cover: only the allowed role can open the page; the list shows the rows and the record count; saving an empty form
  shows field errors without calling the API; a valid form sends the trimmed request, closes and reloads with a toast;
  a server field error shows next to its field; a stale edit shows the conflict message on top; a changed form asks
  before closing; delete asks to confirm and shows a refusal as a toast.
- Test names are sentences: `Admin_cannot_open_the_patients_page`.

## Step 5: Docs and verification

1. Add a row for the feature to the table in `Docs/foundation.md` section 10 (Current bedrock), and tick it in
   `Docs/roadmap.md` if it is listed there.
2. `dotnet build Mostawsef.slnx`: warnings are errors; fix the cause instead of switching rules off.
3. `./scripts/test.ps1 api <Feature>` and `./scripts/test.ps1 desktop <Feature>`, then `./scripts/test.ps1` (API tests
   need Docker). Do not finish with failing tests.
4. Start the API and the desktop app and look at the new page in Arabic and in the Dark theme
   (`scripts/capture-window.ps1` takes a screenshot).
