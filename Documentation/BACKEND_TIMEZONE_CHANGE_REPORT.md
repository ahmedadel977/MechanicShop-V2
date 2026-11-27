# Backend Timezone Change Report — Dashboard Date Filtering

**Date:** 2026-11-27
**Scope:** Dashboard statistics date filtering / timezone handling
**Report location:** `Documentation/BACKEND_TIMEZONE_CHANGE_REPORT.md`

---

## 1. Executive Summary

### 1.1 Problem Statement

The Dashboard statistics endpoint returned metrics under incorrect calendar dates.
Records appeared under the wrong day because of timezone conversion inconsistencies
between the frontend (business timezone: `Africa/Cairo`) and the backend (UTC-based
storage).

### 1.2 Root Cause

- The frontend captured a user-selected calendar date from an `<input type="date">`
  (`yyyy-MM-dd`) and sent it as the query parameter `date` to
  `GET /api/v1.0/dashboard/stats`.
- The backend `GetWorkorderStatsQuery` accepted a `DateOnly Date` and the handler
  converted it to UTC boundaries via:

  ```csharp
  var startsOfDay = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
  var endOfDay = request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
  ```

  This treated the selected business calendar date (Cairo) as if its midnight were
  UTC midnight. For a Cairo day such as `2026-11-27` (UTC+2), the correct UTC boundary is
  `2026-11-26T22:00:00Z`, but the old code used `2026-11-27T00:00:00Z`. The whole day was
  therefore shifted, and records were bucketed under the wrong calendar dates.

### 1.3 Applied Fix

- The frontend is now responsible for converting the selected `Africa/Cairo` calendar day
  into UTC boundaries and sends `fromUtc` / `toUtc`.
- The backend accepts `FromUtc` and `ToUtc` and performs UTC-only range filtering:

  ```csharp
  .Where(x => x.StartAtUtc >= request.FromUtc && x.StartAtUtc < request.ToUtc);
  ```

- No local day boundary calculation remains in the backend.
- UTC storage strategy and entity types were preserved.
- The database continues to store UTC timestamps.

---

## 2. Files Changed

| File Path | Change Type | Description | Reason |
|---|---|---|---|
| `src/MechanicShop.Api/Controllers/DashboardController.cs` | Modified | Accepts `fromUtc` and `toUtc` query parameters (`DateTimeOffset`) and passes them to the query. | New API contract requires explicit UTC boundaries. |
| `src/MechanicShop.Application/Features/Dashbord/Queris/GetWOkorderstats/GetWorkorderStatsQuery.cs` | Modified | Query contract changed from `DateOnly Date` to `DateTimeOffset FromUtc, DateTimeOffset ToUtc`. | Backend must consume explicit UTC range boundaries. |
| `src/MechanicShop.Application/Features/Dashbord/Queris/GetWOkorderstats/GetWorkorderStatsQueryHandler.cs` | Modified | Filtering now uses `x.StartAtUtc >= request.FromUtc && x.StartAtUtc < request.ToUtc`; DTO date derived from `FromUtc`. | UTC-only filtering with inclusive start / exclusive end. |
| `src/MechanicShop.Application/Features/Dashbord/Queris/GetWOkorderstats/GetWorkorderStatsQueryValidator.cs` | Modified | Validates `FromUtc` and `ToUtc` as required and `FromUtc < ToUtc`. | Prevents invalid UTC ranges. |
| `tests/MechanicShop.Api.IntegrationTests/Controllers/DashboardControllerTests.cs` | Modified | Updated requests to `fromUtc` / `toUtc`; added Cairo boundary tests (midnight, previous midnight, next midnight, DST). | Cover new UTC range contract and timezone behavior. |
| `tests/MechanicShop.Application.SubcutaneousTests/Features/Dashboard/Queries/GetWorkOrderStats/GetWorkOrderStatsQueryHandlerTests.cs` | Modified | Handler tests updated to send UTC ranges. | Align tests with new query contract. |
| `tests/MechanicShop.Application.SubcutaneousTests/Features/Dashboard/Queries/GetWorkOrderStats/GetWorkOrderStatsQueryValidator.cs` | Modified | Validator tests updated for `FromUtc` / `ToUtc` rules. | Align tests with new validation rules. |
| `frontend/src/timezone.ts` | Added | Reusable Africa/Cairo ↔ UTC conversion utilities (day range, date key, time value, local ↔ UTC conversion). | Single source of timezone logic. |
| `frontend/src/hooks.ts` | Modified | `useDashboardStats` computes Cairo day UTC range and calls the new stats API. | Frontend owns timezone conversion. |
| `frontend/src/api.ts` | Modified | `dashboardApi.stats(fromUtc, toUtc)` sends `fromUtc` / `toUtc` query params. | New API contract. |
| `frontend/src/App.tsx` | Modified | Cairo-aware display formatting and UTC → Cairo local value conversion for date inputs; schedule timezone default set to `Africa/Cairo`. | Correct display and submission of timestamps. |
| `frontend/src/api.test.ts` | Modified | Dashboard stats test now uses the computed UTC range. | Keep API contract coverage aligned. |
| `frontend/src/timezone.test.ts` | Added | Unit tests for Cairo day boundaries, DST offset, and UTC ↔ Cairo conversions. | Cover timezone conversion behavior. |

---

## 3. Detailed Code Changes

### 3.1 `src/MechanicShop.Api/Controllers/DashboardController.cs`

**Old behavior:**

```csharp
public async Task<IActionResult> GetTodayStats([FromQuery] DateOnly? date, CancellationToken ct)
{
    var statsDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var result = await sender.Send(new GetWorkorderStatsQuery(statsDate), ct);
    return result.Match(response => Ok(response), Problem);
}
```

**New behavior:**

```csharp
public async Task<IActionResult> GetTodayStats(
    [FromQuery(Name = "fromUtc")] DateTimeOffset fromUtc,
    [FromQuery(Name = "toUtc")] DateTimeOffset toUtc,
    CancellationToken ct)
{
    var result = await sender.Send(new GetWorkorderStatsQuery(fromUtc, toUtc), ct);
    return result.Match(response => Ok(response), Problem);
}
```

**Reason:** The endpoint now receives explicit UTC boundaries instead of a bare calendar
date, so the backend no longer derives day boundaries. The `[FromQuery(Name = ...)]`
attributes bind the exact wire names `fromUtc` and `toUtc` used by the frontend.

### 3.2 `src/MechanicShop.Application/Features/Dashbord/Queris/GetWOkorderstats/GetWorkorderStatsQuery.cs`

**Old:**

```csharp
public sealed record GetWorkorderStatsQuery (DateOnly Date) : IRequest<Result<TodayWorkOrderStatsDto>>
```

**New:**

```csharp
public sealed record GetWorkorderStatsQuery (DateTimeOffset FromUtc, DateTimeOffset ToUtc) : IRequest<Result<TodayWorkOrderStatsDto>>
```

**Reason:** The request contract now carries `FromUtc` / `ToUtc`.

### 3.3 `src/MechanicShop.Application/Features/Dashbord/Queris/GetWOkorderstats/GetWorkorderStatsQueryHandler.cs`

**Old:**

```csharp
var startsOfDay = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
var endOfDay = request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
var query = ... .Where(x => x.StartAtUtc >= startsOfDay && x.StartAtUtc < endOfDay);
```

**New:**

```csharp
var query = ... .Where(x => x.StartAtUtc >= request.FromUtc && x.StartAtUtc < request.ToUtc);
```

**Reason:** Backend uses the supplied UTC boundaries directly (inclusive start,
exclusive end) and performs no timezone conversion.

### 3.4 `src/MechanicShop.Application/Features/Dashbord/Queris/GetWOkorderstats/GetWorkorderStatsQueryValidator.cs`

**New validation rules:**

- `FromUtc` must not be the default value → `"FromUtc is required."`
- `ToUtc` must not be the default value → `"ToUtc is required."`
- `FromUtc` must be earlier than `ToUtc` → `"FromUtc must be earlier than ToUtc."`

**Reason:** Guarantees a valid, non-empty UTC range before filtering.

### 3.5 Frontend changes

- `frontend/src/timezone.ts` (new): computes Cairo day boundaries using
  `Intl.DateTimeFormat` with explicit `timeZone: 'Africa/Cairo'` facts (offset-aware,
  DST-aware), e.g. `getCairoUtcDayRange('2026-11-27') → { fromUtc: '2026-11-26T22:00:00Z', toUtc: '2026-11-27T22:00:00Z' }`.
- `frontend/src/hooks.ts`: `useDashboardStats(date)` resolves the Cairo day range and
  calls `dashboardApi.stats(fromUtc, toUtc)`.
- `frontend/src/api.ts`: sends `params: { fromUtc, toUtc }`.
- `frontend/src/App.tsx`: UTC → Cairo display conversions now pass explicit
  `timeZone: 'Africa/Cairo'`; local datetime input values are produced from UTC via the
  Cairo timezone; submission converts Cairo local values back to UTC.

---

## 4. API Contract Changes

- **Affected endpoint:** `GET /api/v1.0/dashboard/stats` (Dashboard statistics).
- **Old request parameter:** `date` (`2026-11-27`).
- **New request parameters:** `fromUtc`, `toUtc` (ISO-8601 UTC, e.g.
  `2026-11-26T22:00:00Z` / `2026-11-27T22:00:00Z`). Both names are bound explicitly with
  `[FromQuery(Name = "...")]` so the wire names match the frontend axios `params` exactly.

**Request example after the fix:**

```
GET /api/v1.0/dashboard/stats?fromUtc=2026-11-26T22:00:00Z&toUtc=2026-11-27T22:00:00Z
```

**Response:** The response DTO (`TodayWorkOrderStatsDto`) keeps the same fields
(`Date`, totals, ratios); only the source of the reported date changed to the supplied UTC
range.

**Breaking change:** Yes, for API consumers that used `GET /api/v1.0/dashboard/stats?date=...`.
All in-repo consumers were updated:

- `frontend/src/api.ts`, `frontend/src/hooks.ts`, `frontend/src/api.test.ts`,
  `frontend/src/App.tsx`.

**Post-change example:**

```
GET /api/v1.0/dashboard/stats?fromUtc=2026-11-26T22:00:00Z&toUtc=2026-11-27T22:00:00Z
```

returns statistics for the Africa/Cairo calendar day `2026-11-27`.

---

## 5. UTC Handling

**Full time-zone cycle:**

1. User selects calendar date `2026-11-27` in the Dashboard (`Africa/Cairo`).
2. Frontend computes Cairo start of day and start of next day (DST-aware via
   `Intl.DateTimeFormat` with `timeZone: 'Africa/Cairo'`) →
   `fromUtc = 2026-11-26T22:00:00Z`, `toUtc = 2026-11-27T22:00:00Z`.
3. Frontend sends `GET /api/v1.0/dashboard/stats?fromUtc=...&toUtc=...`.
4. Backend validates and filters:

   ```csharp
   .Where(x => x.StartAtUtc >= request.FromUtc && x.StartAtUtc < request.ToUtc)
   ```

   (start inclusive, end exclusive).
5. Database still stores UTC (`StartAtUtc` / `EndAtUtc` as `DateTimeOffset`).
6. Frontend converts returned UTC timestamps to `Africa/Cairo` for display via
   `Intl.DateTimeFormat` with explicit `timeZone: 'Africa/Cairo'`.

---

## 6. Validation

New validation behavior for the dashboard stats query:

| Rule | Condition | Error |
|---|---|---|
| FromUtc required | `FromUtc` is default | `FromUtc is required.` |
| ToUtc required | `ToUtc` is default | `ToUtc is required.` |
| Range order | `FromUtc` must be before `ToUtc` | `FromUtc must be earlier than ToUtc.` |

Invalid requests are rejected (validation behavior returns a validation problem → HTTP
400). Boundary semantics: start inclusive, end exclusive.

---

## 7. Testing

### 7.1 Tests added / updated

- Backend integration tests (`tests/MechanicShop.Api.IntegrationTests/Controllers/DashboardControllerTests.cs`):
  - `GetDashboardStats_WhenAuthenticated_ShouldReturnRequestedDate`
  - `GetDashboardStats_WhenDateHasNoWorkOrders_ShouldReturnZeroTotals`
  - `GetDashboardStats_WhenCompletedWorkOrderAndInvoiceExist_ShouldReturnCalculatedTotals`
  - `GetDashboardStats_WhenWorkOrderStartsAtCairoMidnight_ShouldIncludeItInSelectedDate` (new)
  - `GetDashboardStats_WhenWorkOrderStartsBeforeCairoMidnight_ShouldExcludeItFromSelectedDate` (new)
  - `GetDashboardStats_WhenWorkOrderStartsAtNextCairoMidnight_ShouldExcludeItFromSelectedDate` (new)
  - `GetDashboardStats_WhenWorkOrderStartsAtCairoMidnightDuringDaylightSavingTime_ShouldIncludeItInSelectedDate` (new)
- Backend handler tests (`GetWorkOrderStatsQueryHandlerTests.cs`): updated to UTC ranges.
- Backend validator tests (`GetWorkOrderStatsQueryValidator.cs` tests): updated rules coverage.
- Frontend tests (`frontend/src/timezone.test.ts`, `frontend/src/api.test.ts`): added Cairo
  day boundary tests (standard time `2026-11-27 → 22:00Z`, DST `2026-07-15 → 21:00Z`, standard
  `2026-01-15 → 22:00Z`), UTC → Cairo helpers, and API stats range coverage.

### 7.2 Tested scenarios

1. Record at Cairo midnight belongs to the selected Cairo date (`fromUtc` inclusive).
2. Record shortly before Cairo midnight is excluded from the selected date (belongs to the
   previous date).
3. Record shortly before the next Cairo midnight belongs to the selected date.
4. Record exactly at the next Cairo midnight is excluded (exclusive end).
5. UTC boundaries calculated correctly (`Africa/Cairo` → UTC, DST-aware).
6. Filtering works across daylight saving time transitions (July `2026`, UTC+3).
7. Dashboard results consistent with the selected Cairo calendar date.

### 7.3 Actual test run results

- Backend builds (all **SUCCESS**, 0 errors / 0 warnings):
  - `src/MechanicShop.Api/MechanicShop.Api.csproj`
  - `tests/MechanicShop.Api.IntegrationTests/MechanicShop.Api.IntegrationTests.csproj`
  - `tests/MechanicShop.Application.SubcutaneousTests/MechanicShop.Application.SubcutaneousTests.csproj`
- Backend test executions (via Microsoft.Testing Platform test executables; plain
  `dotnet test` does not discover tests in these xUnit v3 / MTP projects in this environment,
  reporting "Zero tests ran"):
  - `tests/MechanicShop.Application.UnitTests/bin/Debug/net10.0/MechanicShop.Application.UnitTests.exe` →
    **Passed!** — total 53, failed 0, succeeded 53, skipped 0, duration 3s 405ms.
  - `tests/MechanicShop.Domain.UnitTests/bin/Debug/net10.0/MechanicShop.Domain.UnitTests.exe` →
    **Passed!** — total 112, failed 0, succeeded 112, skipped 0, duration 509ms.
  - `tests/MechanicShop.Application.SubcutaneousTests/bin/Debug/net10.0/MechanicShop.Application.SubcutaneousTests.exe --filter "FullyQualifiedName~GetWorkOrderStats"` →
    **Passed!** — total 7, failed 0, succeeded 7 (after fixing an invalid `DateTimeOffset`
    construction in the empty-range test).
  - `tests/MechanicShop.Application.SubcutaneousTests/bin/Debug/net10.0/MechanicShop.Application.SubcutaneousTests.exe` (full suite) →
    **Passed!** — total 185, failed 0, succeeded 185, skipped 0, duration 31s 063ms.
  - `tests/MechanicShop.Api.IntegrationTests/bin/Debug/net10.0/MechanicShop.Api.IntegrationTests.exe --filter "FullyQualifiedName~Dashboard"` →
    First run **Failed!** (7/7) due to defects in the newly added test code: invalid
    `DateTimeOffset(DateTime, TimeSpan)` construction (Cairo-midnight boundary tests) and an
    invalid custom `DateTime` format string in the `UtcIso` helper. After fixing the test code →
    **Passed!** — total 7, failed 0, succeeded 7, duration ~22s (executed against SQL Server and
    Redis Testcontainers).
- Full integration suite (no filter) → **Passed!** — total 135, failed 0, succeeded 135,
  skipped 0, duration ~2m 20s (SQL Server + Redis Testcontainers).
- Frontend tests: **NOT EXECUTED in this environment** — no Node.js / npm / pnpm / bun / vitest
  runtime is available; `frontend/src/timezone.test.ts` and the updated `frontend/src/api.test.ts`
  could not be executed here.

### 7.4 Test totals

- `MechanicShop.Application.UnitTests`: **53 passed, 0 failed, 0 skipped**.
- `MechanicShop.Domain.UnitTests`: **112 passed, 0 failed, 0 skipped**.
- `MechanicShop.Application.SubcutaneousTests`: **185 passed, 0 failed, 0 skipped**.
- `MechanicShop.Api.IntegrationTests`: **135 passed, 0 failed, 0 skipped**.
- Dashboard-scoped integration tests: **7 passed, 0 failed, 0 skipped** (Cairo midnight boundaries,
      next-midnight exclusion, DST).
- Frontend: not executed in this environment.

---

## 8. Backward Compatibility

- **Frontend:** Updated consumer (`frontend/src/api.ts`, `hooks.ts`, `App.tsx`, tests) to the
  new `fromUtc` / `toUtc` contract; UI behavior preserved (date picker still sends a Cairo
  calendar day).
- **API consumers:** Breaking change for the dashboard stats request contract (see section 4).
- **Database:** No changes; UTC storage and entity properties preserved.
- **Booking logic:** Unchanged.

---

## 9. Unchanged Components

- Database schema / migrations: unchanged.
- UTC storage strategy: unchanged.
- Work order creation / relocation / state logic: unchanged.
- Other dashboard computations (revenue aggregation, ratios): unchanged apart from receiving
  the new range.

---

## 10. Git Changes Summary

- Modified backend files: `DashboardController.cs`, `GetWorkorderStatsQuery.cs`,
  `GetWorkorderStatsQueryHandler.cs`, `GetWorkorderStatsQueryValidator.cs`,
  dashboard test files.
- Added frontend files: `frontend/src/timezone.ts`,
  `frontend/src/timezone.test.ts`.
- Frontend modified: `frontend/src/api.ts`, `frontend/src/hooks.ts`, `frontend/src/App.tsx`,
  `frontend/src/api.test.ts`.
- Existing unrelated working-tree change observed (not made during this task):
  `.github/workflows/security.yml`.

---

## 11. Final Verification

- [x] Backend build succeeded (`MechanicShop.Api` + related test projects).
- [x] Backend unit tests executed: `MechanicShop.Application.UnitTests` 53/53 passed;
      `MechanicShop.Domain.UnitTests` 112/112 passed.
- [x] Backend subcutaneous tests executed: `MechanicShop.Application.SubcutaneousTests` 185/185 passed
      (after fixing the invalid `DateTimeOffset` construction in one handler test).
- [x] Backend integration tests executed (Dashboard scope): `MechanicShop.Api.IntegrationTests`
      — after fixing the `DateTimeOffset` construction and `UtcIso` format defects in the new tests,
      **7/7 passed** against SQL Server + Redis Testcontainers.
- [x] Full integration suite executed without filter: **135/135 passed** (Dashboard-filtered run
      passed 7/7).
- [ ] Frontend tests executed (blocked: missing Node.js runtime).
- [x] Timezone conversion implementation completed for the dashboard flow.
- [x] Real boundary cases executed (backend integration tests: Cairo midnight include/exclude,
      next-midnight exclude, DST include).
- [x] No unrelated source files intentionally modified during this task.

---

## 12. Final Report Summary (for this task)

- **Root cause:** Backend interpreted the selected Africa/Cairo calendar date as a UTC day
  (`DateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)`), shifting all dashboard
  boundaries and causing records to land under incorrect calendar dates.
- **Fix:** Frontend converts Africa/Cairo day boundaries to UTC (`fromUtc` / `toUtc`);
  backend filters with `StartAtUtc >= FromUtc && StartAtUtc < ToUtc`.
- **Files changed:** 7 backend source/test files + 6 frontend source/test files modified/added.
- **API contract:** `GET /api/v1.0/dashboard/stats?fromUtc=...&toUtc=...` (breaking change).
- **Tests:** backend integration/handler/validator tests updated; frontend timezone tests added.
- **Build results:** backend `dotnet build` succeeded (0 errors) for API + test projects.
- **Executed backend test results (this session):**
  - Application.UnitTests: 53 passed / 0 failed.
  - Domain.UnitTests: 112 passed / 0 failed.
  - SubcutaneousTests (full suite): 185 passed / 0 failed.
  - IntegrationTests Dashboard (after fixing test-code defects): 7 passed / 0 failed.
  - IntegrationTests full suite: 135 passed / 0 failed.
- **Report file:** `D:\MechanicShop\Documentation\BACKEND_TIMEZONE_CHANGE_REPORT.md`.
- **Remaining issues:** frontend tests not executable here (no Node.js); plain `dotnet test` does
  not discover MTP-based backend tests in this environment.
