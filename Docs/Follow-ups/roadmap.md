# Roadmap

✅ = done.

## Before the first business feature

Every feature will copy these patterns, so they're worth settling first.

1. **Validation from server to form.** ✅ There's no validation standard yet. Pick one, for example FluentValidation, and
   return errors in one standard format with an error per field. On the desktop, map those errors onto the matching
   FormField. Every form will need this.
2. **Form view model base.** ✅ Build a reusable base like PagedListViewModel, but for create/edit forms. It should handle:
   - a busy state while saving; ✅
   - Save and Cancel; ✅
   - tracking unsaved changes, with a "discard changes?" prompt when the user navigates away; ✅
   - showing the server's field errors on the form. ✅
3. **One complete example feature.** ✅ Build one non-business module end to end as the template: entity, configuration,
   service, controller, DTOs, a list page, a create/edit form and tests. User management fits well, since it's missing
   anyway: create and disable users, change your password, reset someone else's password. After that, every business
   feature copies this pattern.
4. **Base entity fields.** ✅ Add these to Entity now, because adding them later means migrating every table:
   - CreatedBy / UpdatedBy, for who changed what; ✅
   - a Version number to detect conflicting edits. Two receptionists on the clinic network editing the same patient
     should get a clear "someone else changed this" message, not a silent overwrite. Use a plain integer column rather
     than PostgreSQL's built-in row version, so the SQLite fallback still works; ✅
   - a deletion policy: medical records usually shouldn't be hard-deleted, so decide on soft delete or archiving with a
     global filter. ✅
5. **Arabic search.** ✅ You can't use case-insensitive PostgreSQL functions, because of the SQLite fallback. Store a
   normalized search column instead, treating أ/إ/آ as ا, ة as ه and ى as ي, and removing diacritics. Without this,
   patient search will miss names.
6. **Readable numbers.** ✅ Things like patient file numbers (P-1001) and invoice numbers need a counter that works on both
   PostgreSQL and SQLite.
7. **Dates, time and money.** ✅
   - Use .NET's TimeProvider instead of DateTime.UtcNow, so tests can control the clock. ✅
   - Use DateOnly for birth dates. ✅
   - Decide whether Arabic screens show Arabic or Latin digits. ✅
   - Add one shared formatter for amounts stored as integers in piasters. ✅
8. **Clinic settings.** A single settings record holding the clinic's name, logo, address, phone and currency. Printouts
   and page headers will need it.
9. **Reference lists.** ✅ Decide when a fixed list is a code enum and when it's an editable table with Arabic and English
   names (for example specialties, drugs, services).

## Before the first clinic installation

10. **Server connection status.** ✅
    - Check /api/health regularly and show a "connection lost" banner that clears when the server comes back. ✅
    - When the login session expires, take the user cleanly back to sign-in. ✅
    - Check that the desktop and server versions match, since computers on the clinic network can drift apart. ✅
11. **Printing.** ✅ Prescriptions, receipts and invoices need correct Arabic right-to-left layout, which most PDF libraries
    handle badly. Pick and test a library early; QuestPDF is a good candidate.
12. **How the app is deployed.**
    - Run the backend as a Windows service.
    - Install PostgreSQL natively.
    - Let the desktop app point to a server address, so one PC acts as the server and the others connect to it.
    - Add a first-run setup that generates a random login-token secret per clinic and creates the first Super user.
13. **Backups and restore.** Run scheduled pg_dump backups and test that restoring actually works. This matters most for
    medical data.
14. **Installer and updates.** Package everything as one installer, and choose an update method, for example Velopack.

## Developer workflow and tidying up

15. **Shared build settings.** ✅ Add Directory.Build.props, .editorconfig, central package versions and analyzers, and treat
    warnings as errors.
16. **Automatic tests.** ✅ Set up continuous integration so tests run on every push.
17. **New-feature scaffold.** ✅ A Cursor rule or skill that generates a new feature by copying the example feature from item 3.
18. **Docs and README.**
    - Docs/foundation.md is out of date: sections 10 and 12 still say there are no entities or migrations yet. ✅
    - The README it mentions doesn't exist.
    - Add how to put dotnet on the terminal's PATH to Docs/commands.md.
19. **Large test data.** Seed something like 100,000 patients in development to find slow lists and missing database
    indexes before clinics do.
