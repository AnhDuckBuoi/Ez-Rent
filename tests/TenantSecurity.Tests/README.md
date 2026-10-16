# Tenant security integration tests

Run from the repository root:

```powershell
dotnet test tests/TenantSecurity.Tests/TenantSecurity.Tests.csproj
```

Each test starts an independent MVC TestServer with a fresh in-memory SQLite database, two tenants and a landlord. It exercises the application's controllers and compiled Razor views through HTTP, including role authorization, model binding, ownership queries and real anti-forgery tokens.

The test host never executes application `Program.cs` or connects to the group's SQL Server. The header authentication handler is registered only in this test host; it must never be registered in the production application. Most ownership tests use that header handler. `TenantAccountCookieTests` additionally exercises the original Identity login page, cookie refresh, password change and logout with real Identity cookies; live Google OAuth is not tested.

See `docs/tenant-security-review.md` for scope, results and the unresolved authorization gap in the Landlord routes.

The suite now includes rental discovery, combined filters/sorting/paging, unavailable favorites, safe media, HTML encoding and idempotent favorite mutations. The discovery implementation and browser verification are documented in `docs/tenant-discovery-plan.md`.

For a local browser review of the real MVC/Razor pages with a separate in-memory demo database:

```powershell
dotnet run --project tests/TenantSecurity.Tests/TenantSecurity.Tests.csproj -- --preview
```

Open `http://127.0.0.1:8770/Tenant/RentalPost`. This mode binds only to loopback, supplies a demo Tenant in the test host, limits navigation to Tenant modules and the Identity login page and labels the pages as demo data. It never runs the application's `Program.cs` or uses the group's SQL Server. Do not copy its test authentication or demo identity middleware into production. Demo data resets on host restart.

The preview also includes Tenant Home, notices, appointments, booking UI and Messages at `/Tenant/Dashboard/Messages`. Messages reads the original Conversation/Message model and adds no write endpoint. The messaging checkpoint had 133 passing cases, including 14 messaging cases. See `docs/tenant-messages-mvc.md`. Preview conversation fixtures and the illustrative photo exist only in the independent SQLite host.


## Review all current Tenant screens at three sizes

Open `http://127.0.0.1:8770/review/tenant` after starting `--preview`. Choose Desktop (1440×1000), Tablet (834×1112), or Mobile (390×844). The iframe uses the real MVC pages on the same origin, preserving SAMEORIGIN anti-forgery protection. Resizing keeps the current page and in-tab drafts. The page selector opens Home, discovery, saved rooms, appointments, messages, notifications and help; ordinary links reach room details and booking UI. The 100% control uses actual CSS pixel size with horizontal scrolling when necessary.

Direct review presets: `?device=desktop`, `?device=tablet`, `?device=mobile`; optional `&page=messages` (or home/search/saved/appointments/notifications/help). This is viewport simulation, not a native-device or virtual-keyboard emulator. The viewer exists only in the independent preview host; production routes are unchanged.

Build and 23 browser checks passed for this review tool, including 7 pages × 3 sizes, discovery → details → booking, draft preservation and the 100% control. Save/unsave was also verified inside the frame with CSRF and the initial state restored. Evidence: `tenant-app-responsive-review.json` and `tenant-app-review-{desktop,tablet,mobile}.png` in the chat artifacts. Business limits of booking/message sending remain as documented for those modules.


## Tenant account

The review selector now includes `account` and `security`. See `docs/tenant-account-mvc.md` for the implementation map and manual review. Account QA: the full suite passed 152/152; after adding one JPEG test, the final account confirmation passed 19/19 (153 cases now defined). Browser QA passed 13 checks. The preview uses real Identity cookies and signs into the demo only once per browser session; logout remains effective. Demo login: `minhanh@example.test` / `TenantDemo@123`. These credentials exist only in the isolated in-memory fixture. Profile/password changes persist until the preview host restarts. Clear localhost cookies after restarting the host if an old session no longer matches the new database.
