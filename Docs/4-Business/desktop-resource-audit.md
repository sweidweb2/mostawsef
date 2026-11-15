# Desktop Resource Audit

Source-inspection audit of `frontend/Mostawsef.Desktop` (Avalonia, .NET 10, CommunityToolkit.Mvvm, QuestPDF). Windows-first.

> **Limit:** apart from the export timing in Block 6, nothing here was measured. RAM, CPU, startup time and disk I/O need runtime profiling. Every "benefit" below is an expectation, not a result.

Work the blocks in order. ✅ = done. ⏳ = waiting for a real-machine run. Open checkboxes inside a block are real-machine checks.

## Status

| Block | Finding | Severity | Type | Status |
|---|---|---|---|---|
| 0 | Measure a baseline | - | Profiling | ⏳ Targets set, needs a real run |
| 1 | Local logs never pruned | Medium | Confirmed | ✅ Done |
| 2 | Printed PDFs (patient data) left in Temp | Medium | Confirmed | ✅ Done |
| 3 | API base URL defaults to plain HTTP | Medium | Confirmed (deployment-dependent) | ✅ Warning added; HTTPS setup is a deployment step |
| 4 | Page view cache lives for the whole session | Medium | One leak path fixed; heap check still needed | ✅ Code fixed |
| 5 | Warm-up and prebuild cost on slow machines | Medium | Off-switch added; default unchanged | ✅ Switch added |
| 6 | Export holds rows plus PDF in memory | Low-Medium | Measured: no change needed | ✅ Done, no code change |
| 7 | Lab test catalog fully cached | Low | No action | ✅ No action needed |
| 8 | Connection monitor polls every 60 s | Low | No action | ✅ No action needed |
| 9 | Performance monitor in Release | Low | No action | ✅ No action needed |

---

## Block 0 - Measure a baseline first

Several blocks (4, 5, 6) depend on numbers. Do this before changing anything.

- **Machine:** the oldest clinic PC (HDD, 4 GB RAM if one exists), Release publish build.
- **Tools:** Task Manager, `dotnet-counters`, `dotnet-gcdump`, and the app's own perf log (`MOSTAWSEF_PERF=1` writes `%AppData%\Mostawsef\logs\perf-yyyyMMdd.log`).

| Scenario | Indicator | Tool |
|---|---|---|
| Cold start to login screen, then to the portal after sign-in | time to interactive | perf log or stopwatch |
| Idle signed in 30+ min, minimized and restored | idle CPU, working set | Task Manager, `dotnet-counters` |
| Open and close 100 orders and forms | managed heap, `PageHost` view count | `dotnet-gcdump` |
| Export at the maximum row count | peak working set, duration, UI responsiveness | Task Manager, perf log |
| Warm-up on vs off | startup time, memory | same as above |
| API offline for an hour | log size, CPU | file size, Task Manager |

**No measurements yet.** Until a real run exists, use these common targets for desktop apps on older PCs as a pass/fail line. They are rules of thumb, not measurements of this app:

| Indicator | Target |
|---|---|
| Cold start to login screen | under 3 s |
| Sign-in to usable portal | under 2 s |
| Idle CPU, signed in | under 1% |
| Working set after a long session | under about 300 MB, and flat over time |
| Page switch | under 200 ms (the perf monitor marks 200 ms amber and 500 ms red) |
| Maximum export | UI never frozen; peak memory under about 500 MB |
| Local storage (logs and PDFs) | stays flat over weeks |

**Done when:** a table of real numbers is saved next to this file and compared with the targets above.

---

## Block 1 - Local logs are never pruned ✅

- **Category:** Disk and storage
- **Severity:** Medium
- **Status:** Confirmed
- **Files:**
  - `frontend/Mostawsef.Desktop/Services/Diagnostics/ErrorReporter.cs` (lines 229, 248, 257)
  - `frontend/Mostawsef.Desktop/Services/Diagnostics/PerformanceMonitor.cs` (lines 95, 325)

**Current behavior.** `errors-yyyyMMdd.log` and `perf-yyyyMMdd.log` are appended under `%AppData%\Mostawsef\logs`. No code deletes old files.

**Why it is a problem.** Each error entry holds a full `exception.ToString()`. The log fills fastest when the API is offline, because reports fall back to the file. On a small disk it grows without limit.

**Fix.** On startup, delete log files older than about 14-30 days. Run it once on a background thread. Use the same pattern as `PrintService.DeleteOldPrints`, and ignore `IOException` for files in use.

**Benefit.** Bounded local storage. No speed change.

**Verify.** Create backdated log files, start the app, confirm they are gone and recent ones stay.

**Implemented.** `Services/Local/LocalFileCleanup.cs` deletes `*.log` files older than 30 days from the log folder. It runs once at startup on a background thread (`App.axaml.cs`). A locked or undeletable file is skipped and retried next launch. Unit tests: `tests/Mostawsef.Desktop.Tests/Local/LocalFileCleanupTests.cs`.

- [x] Implemented
- [x] Verified by unit tests (real-machine check still optional)

---

## Block 2 - Printed PDFs with patient data stay in Temp ✅

- **Category:** Security and local data, disk
- **Severity:** Medium
- **Status:** Confirmed
- **Files:** `frontend/Mostawsef.Desktop/Services/Printing/PrintService.cs` (lines 66, 91-101, 105-118)

**Current behavior.** PDFs are written to `%TEMP%\Mostawsef\print`. Cleanup (files older than 1 day) runs only inside `PrintAsync`. If nobody prints again, nothing is removed. A file still open in the viewer cannot be deleted.

**Why it is a problem.** The PDFs hold patient names, phones and results in plain form, readable by any process running as that user. This is a privacy exposure on shared clinic PCs and leaves clutter.

**Fix.** Also sweep the folder on app start and on exit. Treat one day as a maximum. If the requirement is strict, delete after the viewer closes.

**Benefit.** Less leftover patient data and disk use.

**Verify.** Print, close the app, check whether the file remains. After the fix it should be gone, or swept at the next launch.

**Implemented.** The same class also deletes printed PDFs older than 1 day. It runs before each print (as before), at startup, and at app exit, so files no longer wait for the next print. The old private `DeleteOldPrints` in `PrintService` was removed. Limit: a PDF younger than a day still stays until the next launch or exit, and a file open in a viewer cannot be deleted until it closes.

- [x] Implemented
- [x] Verified by unit tests and the existing print cleanup test

---

## Block 3 - API base URL defaults to plain HTTP ✅

- **Category:** Security
- **Severity:** Medium if the API runs on another PC; none if it is on the same machine
- **Status:** Confirmed default; deployment impact unknown
- **Files:** `frontend/Mostawsef.Desktop/Services/Local/AppSettings.cs` (line 22)

**Current behavior.** `ApiBaseUrl` defaults to `http://localhost:5270`. The access token is held only in memory (`SessionService`) and never written to disk, which is good.

**Why it is a problem.** If the API is on another machine over HTTP, the JWT and patient data cross the LAN unencrypted.

**Fix.** Use HTTPS for any non-local deployment and document that choice.

**Verify.** Check how each clinic sets `ApiBaseUrl`.

**Implemented.** The default stays `http://localhost:5270`, which is safe on one PC. When `ApiBaseUrl` is plain http to another machine, the login screen shows a warning banner (English and Arabic). The check is `Services/Local/ApiAddress.IsInsecureRemote`, tested in `tests/Mostawsef.Desktop.Tests/Local/ApiAddressTests.cs`. `Docs/auth.md` and deploy-install Block 4 record the install decision: **Option A — one-PC localhost HTTP only**; HTTPS (Option B) is deferred until a multi-PC install is planned.

- [x] Warning implemented and tested
- [x] HTTPS guidance documented
- [x] Install decision recorded (one PC / Option A)

---

## Block 4 - Page view cache lives for the whole session ✅

- **Category:** RAM
- **Severity:** Medium (potential)
- **Status:** By design; needs profiling
- **Files:**
  - `frontend/Mostawsef.Desktop/Components/Layout/PageHost.cs` (lines 30-31, 48-75, 140-155)
  - `frontend/Mostawsef.Desktop/Views/Shell/PortalShellView.axaml.cs` (line 68)

**Current behavior.** `PageHost` caches each page's view and toggles visibility, because detaching a `DataGrid` with rows was very slow. Views are dropped only on a language switch (except the visible one) or when a transient sub-page calls `Release`. Menu pages and their cached rows stay for the session.

**Why it matters.** This trades memory for speed on purpose, and the count is bounded by the number of menu pages. A leak exists only if some discard path skips `Release`. `Release` is wired to `PageDiscarded`, but I did not trace every discard path.

**Recommendation.** Do not change it blindly. Take two heap snapshots after a long session of opening and closing orders and forms. Fix only if the view count keeps rising.

**Verify.** Open and close 100 order-detail pages. Heap and `PageHost._views.Count` should return to baseline.

**Traced and fixed.** Every discard path goes through `PortalShellViewModel.DiscardSubPage`, which tells `PageHost` to drop the view, except for pages marked `IReusablePage` (New order, Order details). Those are meant to be created once by `OrdersViewModel` and reused. One path broke that assumption: `NewOrderViewModel.OnClosed` falls back to `OpenAsync<OrderDetailsViewModel>(...)`, which creates a fresh instance. Its view was never released, so each use would have left one view in memory. `OpenAsync<T>(setup)` now marks the page as a new instance and the shell releases its view on close. In normal use this fallback is rare (it applies only when New order was opened without the Orders page), so the real-world gain is small. Tests: `tests/Mostawsef.Desktop.Tests/Shell/SubPageReleaseTests.cs` (a new instance is released; a reused instance keeps its view).

**Left alone, by design.** Menu page views stay cached for the session, and the reused New order and Order details views stay too. That is bounded by the number of menu pages plus those two, and it is what keeps page switches fast. Sign-out closes the portal and drops the menu pages.

**Still needs a real run.** Whether memory stays flat over a long session can only be shown with a heap snapshot (Block 0).

- [x] Discard paths traced
- [x] Leak path fixed and tested
- [ ] Heap check after a long session (needs a real run)

---

## Block 5 - Warm-up and prebuild cost on slow machines ✅

- **Category:** Startup, low-spec PCs
- **Severity:** Medium (potential)
- **Status:** Needs profiling
- **Files:**
  - `frontend/Mostawsef.Desktop/Views/ViewWarmUp.cs` (lines 48-95)
  - `frontend/Mostawsef.Desktop/Views/Shell/PortalShellView.axaml.cs` (lines 86-125)
  - `frontend/Mostawsef.Desktop/Services/Printing/PrintService.cs` (lines 48-62)

**Current behavior.** While the login screen is open, about 15-25 views are built and laid out in an invisible layer, one per Background dispatcher job. After sign-in, every menu page is prebuilt the same way. A throwaway PDF render runs on a thread-pool thread. The code documents about 800 ms to 250 ms for the portal appearing, measured on one machine.

**Why it matters.** Work is split into small Background jobs, so typing still goes first. But on an old CPU or HDD it adds JIT, layout and allocation right when the user starts work, and prebuilt views stay in memory (Block 4). The gain was measured on fast hardware and may differ on slow hardware.

**Recommendation.** Keep it. Measure on a weak machine with it on and off. Only if it hurts there, make the page count or the whole warm-up configurable. Add no adaptive logic without numbers.

**Verify.** Compare time to interactive and working set, on and off, on the oldest PC.

**Implemented (switch only).** `PreloadViews` in `%AppData%\Mostawsef\settings.json` (default `true`). Set it to `false` to skip both the login-screen warm-up and the after-sign-in menu prebuild. The PDF warm-up for Admin is separate and unchanged. The default is unchanged because there are no measurements showing the preload hurts. I did not add automatic detection of slow PCs: it would be a guess without numbers.

**To decide with data:** on the oldest PC, compare time to interactive and working set with `PreloadViews` true and false (Block 0). If false feels better there, flip the default or set it per clinic.

- [x] Switch added
- [ ] Measured on the oldest PC
- [ ] Decision: keep default / change

---

## Block 6 - Export holds rows and PDF in memory ✅

- **Category:** RAM, UI responsiveness
- **Severity:** Low to Medium
- **Status:** Needs profiling with large data
- **Files:**
  - `frontend/Mostawsef.Desktop/ViewModels/Admin/PatientsViewModel.cs` (lines 123-150)
  - `frontend/Mostawsef.Desktop/Services/Printing/PrintService.cs` (lines 86-103)

**Current behavior.** Export fetches up to `PageQuery.MaxExportRows` rows, builds the QuestPDF document, renders to a `byte[]` on a background task, then writes with `File.WriteAllBytes`. Rendering is off the UI thread. A toast warns when the cap is hit.

**Why it matters.** Cost scales with the cap: rows plus PDF bytes plus QuestPDF layout. At the cap on a low-RAM PC this is probably the app's peak memory. Source alone cannot say whether it is a problem.

**Recommendation.** Test a maximum-size export on a low-RAM machine and record the peak.

**Verify.** Peak working set, duration, and whether the UI stays responsive during the export.

**Measured (Release build, headless test host on the development PC, patient list PDF; one run each, so treat as approximate).** The 10,000-row cap is `PageQuery.MaxExportRows`.

| Rows | Render time | PDF size | Extra managed memory | Peak working set of the whole test process |
|---|---|---|---|---|
| 1,000 | 0.3-0.4 s | 0.2 MB | about 4 MB | about 215-230 MB |
| 5,000 | 1.6-1.9 s | 1.1 MB | about 24 MB | about 215-230 MB |
| 10,000 (the cap) | 3.9-4.8 s | 2.2 MB | about 48 MB | about 215-285 MB |

The cost grows in a straight line with the row count. The extra memory at the cap is about 50 MB on top of the app, and the PDF is 2 MB, so memory is not a problem even on a 4 GB PC. The working-set column includes the test host itself, so it is an upper bound for the export, not a figure for the real app. Time is the visible cost: about 4 s on this PC, and probably several times longer on an old CPU (not measured). It runs off the UI thread, and the Export command cannot be started twice while it runs (the async command disables its button).

**Decision: no code change.** Lowering the cap or streaming the PDF would add complexity to save about 50 MB. A 10,000-patient list is also a PDF of hundreds of pages that nobody prints, and the app already warns when the cap is hit. If an old PC shows a long wait, the cheaper fix is a busy indicator on the Export button, not a lower cap.

**Still open:** time the 10,000-row export on the oldest PC.

- [x] Measured on the development PC
- [x] Decision: leave as is
- [ ] Time on the oldest PC

---

## Block 7 - Lab test catalog fully cached (no action) ✅

- **Severity:** Low
- **Files:**
  - `frontend/Mostawsef.Desktop/Services/Local/LabTestCatalog.cs` (lines 32-50)
  - `frontend/Mostawsef.Desktop/Services/Api/LabTestsApi.cs` (lines 28-52)

The active catalog is fetched in pages of 100 and kept until the catalog version changes or the user signs out. This avoids a request per keystroke in New Order. A few hundred tests is a small download. Keep it. Revisit only if the catalog passes about 1,000 tests. Log the count and heap size at the largest clinic to confirm.

- [x] Reviewed

---

## Block 8 - Connection monitor polls every 60 s (no action) ✅

- **Severity:** Low
- **Files:** `frontend/Mostawsef.Desktop/Services/Api/ConnectionMonitor.cs` (lines 60-80, 160-170)

One `DispatcherTimer` makes one small `GET /api/health` per minute while signed in. It does not pause when minimized. The cost is negligible, it cancels cleanly on sign-out, and pausing would delay the offline banner. Leave it. Confirm idle CPU is close to 0% in Block 0.

- [x] Reviewed

---

## Block 9 - Performance monitor in Release (no action) ✅

- **Severity:** Low
- **Files:** `frontend/Mostawsef.Desktop/Services/Diagnostics/PerformanceMonitor.cs` (lines 36-45, 130-140, 320-335)

On in Debug; in Release only when `MOSTAWSEF_PERF=1`. When on, it holds `Process.GetCurrentProcess()` and appends a line per event, calling `Directory.CreateDirectory` before each append. In normal Release use it is a no-op. It is the tool for Block 0. Just make sure the variable is not set on production PCs, otherwise Block 1 grows faster.

- [x] Reviewed

---

## What was checked and found fine

- **Timers and subscriptions.** Only two long-lived timers: `ConnectionMonitor` and the session expiry timer. Both stop on sign-out. `PageHost` unsubscribes from language changes when detached. `LocalizationService` holds windows by `WeakReference`. Singleton services subscribe to `SessionService.Changed` for the process lifetime, which is fine.
- **Background tasks.** Warm-up and prebuild take cancellation tokens and stop on sign-out. `Task.Run` is used only for the PDF warm-up and the PDF write.
- **HTTP.** One lazily created `HttpClient`, 3 s connect timeout, 15 s request timeout, so a switched-off API PC fails fast.
- **Lists.** Server-paged with capped totals. The lab test list uses a virtualizing items control.
- **Assets.** About 1.2 MB (four fonts). `ReadyToRun` is on for publish.
- **Settings writes.** Temp file plus `File.Move`, one coalesced writer, 3 s flush timeout on exit. Safe against corruption and a stalled disk.
- **Error handling.** `AppDomain`, `TaskScheduler` and `Dispatcher` handlers route to `ErrorReporter`, with de-duplication, a toast limit and a local file fallback when the API is unreachable.
- **Shutdown.** Settings flush on `Exit`. No child processes or threads found that outlive the app. The PDF viewer is a separate process by design.

## Not reviewed in depth

Every view model and `.axaml` was not read. These could hide issues:

- The New Order page (largest view model, about 800 lines).
- Specific list templates.
- Animations and transitions (about 8 in styles). Reduce them only if profiling shows a cost.
- Avalonia's software-rendering fallback on old integrated graphics.

## Overall assessment

- **Resource use:** lean by design (server paging, cancellation, bounded caches, lazy services). No confirmed leak and no UI-thread blocking found in the paths read.
- **Main risks:** the session-long view cache, startup work tuned on fast hardware, and unbounded local logs and leftover PDFs.
- **Older computers:** probably acceptable but unproven. Unknowns are warm-up cost on a slow CPU or HDD, peak memory of a maximum export, and rendering on old graphics.

## Suggested order

1. Block 0 (baseline).
2. Block 1 (prune logs). Cheap.
3. Block 2 (sweep print folder). Cheap.
4. Block 3 (HTTPS decision).
5. Block 4 (memory snapshots).
6. Blocks 5 and 6, only if the numbers justify changes.
7. Blocks 7-9 need no action.
